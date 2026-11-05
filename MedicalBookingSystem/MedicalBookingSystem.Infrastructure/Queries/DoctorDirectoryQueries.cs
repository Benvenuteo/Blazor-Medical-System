using System.Globalization;
using System.Text.Json;
using MedicalBookingSystem.Application.Queries;
using MedicalBookingSystem.Domain.Scheduling;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure.Queries;

public sealed class DoctorDirectoryQueries(IDbContextFactory<ApplicationDbContext> contexts) : IDoctorDirectoryQueries
{
    public async Task<PagedResult<DoctorDto>> SearchAsync(SearchDoctorsDto search, IReadOnlyList<DateTime> candidateStarts)
    {
        using var context = contexts.CreateDbContext();
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        connection.CreateCollation("MEDICAL_NOCASE", (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        // SQLite has no trigonometry by default; the connection re-registers this function on every open.
        connection.CreateFunction<double, double, double, double, double>("haversine", GeoDistance.Kilometres, isDeterministic: true);
        var hasLocation = search.HasLocation;
        var latitude = search.Latitude ?? 0;
        var longitude = search.Longitude ?? 0;
        var radius = search.RadiusKm ?? MapDefaults.DefaultRadiusKm;
        var box = GeoDistance.BoundingBox(latitude, longitude, radius);
        // Generate candidates only inside real shifts. The tiny exclusion list preserves the
        // application's DST rules without scanning every half-hour in the entire booking horizon per doctor.
        var available = candidateStarts.ToHashSet();
        var first = candidateStarts.Count == 0 ? DateTime.MaxValue.Date : candidateStarts[0];
        var last = candidateStarts.Count == 0 ? first : candidateStarts[^1];
        var excluded = new List<string>();
        for (var start = first; start < last; start = start.AddMinutes(30))
            if (!available.Contains(start)) excluded.Add(SqlDate(start));
        var query = context.Database.SqlQueryRaw<DirectoryRow>("""
            WITH RECURSIVE
            NearbyClinics AS MATERIALIZED (
                SELECT c."Id", haversine($lat, $lng, c."Latitude", c."Longitude") AS "Distance" FROM "Clinics" c
                WHERE $hasLocation AND c."Latitude" BETWEEN $minLat AND $maxLat AND c."Longitude" BETWEEN $minLng AND $maxLng
                  AND haversine($lat, $lng, c."Latitude", c."Longitude") <= $radius
            ),
            EligibleDoctors AS MATERIALIZED (
                SELECT * FROM "Doctors" d
                WHERE ($region IS NULL OR d."Region" = $region COLLATE MEDICAL_NOCASE)
                  AND (NOT $hasLocation OR EXISTS (SELECT 1 FROM "DoctorClinics" dc JOIN NearbyClinics n ON n."Id" = dc."ClinicId"
                       WHERE dc."DoctorId" = d."Id"))
                  AND ($specialization IS NULL OR EXISTS (SELECT 1 FROM "DoctorSpecializations" ds
                       WHERE ds."DoctorId" = d."Id" AND ds."SpecializationId" = $specialization))
            ),
            ShiftBounds AS (
                SELECT s."DoctorId", MAX(s."StartTime", $first) AS "First", s."EndTime" AS "ShiftEnd"
                FROM "DoctorSchedules" s JOIN EligibleDoctors d ON d."Id" = s."DoctorId"
                WHERE $hasStarts AND s."IsAvailable" = 1 AND s."StartTime" <= $last
                  AND (NOT $hasLocation OR s."ClinicId" IN (SELECT "Id" FROM NearbyClinics))
                  AND s."EndTime" >= datetime($first, '+30 minutes')
            ),
            ShiftBuckets AS (
                SELECT *, substr("First", 1, 14) || CASE WHEN CAST(substr("First", 15, 2) AS INTEGER) < 30
                    THEN '00:00' ELSE '30:00' END AS "Bucket" FROM ShiftBounds
            ),
            AlignedShifts AS (
                SELECT "DoctorId", "ShiftEnd", CASE WHEN "First" > "Bucket"
                    THEN datetime("Bucket", '+30 minutes') ELSE "Bucket" END AS "StartTime" FROM ShiftBuckets
            ),
            Candidates("DoctorId", "StartTime", "ShiftEnd") AS (
                SELECT "DoctorId", "StartTime", "ShiftEnd" FROM AlignedShifts
                WHERE "StartTime" <= $last AND datetime("StartTime", '+30 minutes') <= "ShiftEnd"
                UNION ALL
                SELECT "DoctorId", datetime("StartTime", '+30 minutes'), "ShiftEnd" FROM Candidates
                WHERE datetime("StartTime", '+30 minutes') <= $last AND datetime("StartTime", '+60 minutes') <= "ShiftEnd"
            ),
            FreeSlots AS MATERIALIZED (
                SELECT c."DoctorId", MIN(c."StartTime") AS "NextAvailableSlot" FROM Candidates c
                WHERE c."StartTime" NOT IN (SELECT value FROM json_each($excluded))
                  AND NOT EXISTS (SELECT 1 FROM "Appointments" a
                    WHERE a."DoctorId" = c."DoctorId" AND a."Status" <> 'Cancelled'
                      AND a."Date" < datetime(c."StartTime", '+30 minutes')
                      AND a."Date" > datetime(c."StartTime", '-30 minutes'))
                GROUP BY c."DoctorId"
            ),
            RankedClinics AS (
                SELECT dc."DoctorId", c."Id" AS "ClinicId", c."Name", c."Street", c."PostalCode", c."City", c."Latitude", c."Longitude",
                    n."Distance",
                    ROW_NUMBER() OVER (PARTITION BY dc."DoctorId" ORDER BY COALESCE(n."Distance", 0), c."Id") AS "Rank"
                FROM "DoctorClinics" dc
                JOIN EligibleDoctors e ON e."Id" = dc."DoctorId"
                JOIN "Clinics" c ON c."Id" = dc."ClinicId"
                LEFT JOIN NearbyClinics n ON n."Id" = c."Id"
                WHERE NOT $hasLocation OR n."Id" IS NOT NULL
            )
            SELECT d."Id", d."FirstName", d."LastName", d."LicenseNumber", d."Region", d."Bio", d."ImageUrl",
                COALESCE((SELECT AVG(CAST(r."Rating" AS REAL)) FROM "Reviews" r WHERE r."DoctorId" = d."Id"), 0.0) AS "AverageRating",
                (SELECT COUNT(*) FROM "Reviews" r WHERE r."DoctorId" = d."Id") AS "ReviewCount", f."NextAvailableSlot",
                rc."ClinicId", rc."Name" AS "ClinicName", rc."Street" || ', ' || rc."PostalCode" || ' ' || rc."City" AS "ClinicAddress",
                rc."Latitude" AS "ClinicLatitude", rc."Longitude" AS "ClinicLongitude", rc."Distance" AS "DistanceKm"
            FROM EligibleDoctors d LEFT JOIN FreeSlots f ON f."DoctorId" = d."Id"
            LEFT JOIN RankedClinics rc ON rc."DoctorId" = d."Id" AND rc."Rank" = 1
            """, new SqliteParameter("$first", SqlDate(first)), new SqliteParameter("$last", SqlDate(last)),
            new SqliteParameter("$hasStarts", candidateStarts.Count > 0), new SqliteParameter("$excluded", JsonSerializer.Serialize(excluded)),
            new SqliteParameter("$region", string.IsNullOrWhiteSpace(search.Region) ? DBNull.Value : search.Region.Trim()),
            new SqliteParameter("$specialization", (object?)search.SpecializationId ?? DBNull.Value),
            new SqliteParameter("$hasLocation", hasLocation), new SqliteParameter("$lat", latitude), new SqliteParameter("$lng", longitude),
            new SqliteParameter("$radius", radius), new SqliteParameter("$minLat", box.MinLatitude), new SqliteParameter("$maxLat", box.MaxLatitude),
            new SqliteParameter("$minLng", box.MinLongitude), new SqliteParameter("$maxLng", box.MaxLongitude));
        if (search.Date.HasValue) query = query.Where(d => d.NextAvailableSlot != null);
        var total = await query.CountAsync();
        var ordered = search.SortBy?.ToLowerInvariant() switch
        {
            "rating" => query.OrderByDescending(d => d.AverageRating).ThenBy(d => d.Id),
            "availability" => query.OrderBy(d => d.NextAvailableSlot == null).ThenBy(d => d.NextAvailableSlot).ThenBy(d => d.Id),
            "distance" => query.OrderBy(d => d.DistanceKm == null).ThenBy(d => d.DistanceKm).ThenBy(d => d.Id),
            _ => query.OrderBy(d => d.Id)
        };
        var rows = await ordered.Skip((search.Page - 1) * search.PageSize).Take(search.PageSize).ToListAsync();
        return new PagedResult<DoctorDto>(rows.Select(d => new DoctorDto
        {
            Id = d.Id, FirstName = d.FirstName, LastName = d.LastName, LicenseNumber = d.LicenseNumber,
            Region = d.Region, Bio = d.Bio, ImageUrl = d.ImageUrl, AverageRating = (decimal)d.AverageRating,
            ReviewCount = d.ReviewCount, NextAvailableSlot = d.NextAvailableSlot,
            ClinicId = d.ClinicId, ClinicName = d.ClinicName, ClinicAddress = d.ClinicAddress,
            ClinicLatitude = d.ClinicLatitude, ClinicLongitude = d.ClinicLongitude,
            DistanceKm = d.DistanceKm is double km ? Math.Round(km, 1) : null
        }).ToList(), total, search.Page, search.PageSize);
    }

    public async Task<DoctorDetailsDto?> GetDetailsAsync(int doctorId, DateTime from, DateTime to)
    {
        using var context = contexts.CreateDbContext();
        return await context.Doctors.AsNoTracking().Where(d => d.Id == doctorId).Select(d => new DoctorDetailsDto
        {
            Id = d.Id, FirstName = d.FirstName, LastName = d.LastName, LicenseNumber = d.LicenseNumber,
            Region = d.Region, Bio = d.Bio, ImageUrl = d.ImageUrl,
            AverageRating = (decimal)(d.Reviews.Average(r => (double?)r.Rating) ?? 0), ReviewCount = d.Reviews.Count,
            Specializations = d.DoctorSpecializations.OrderBy(s => s.Specialization.Name).Select(s => new SpecializationDto
                { Id = s.SpecializationId, Name = s.Specialization.Name, Description = s.Specialization.Description }).ToList(),
            Schedules = d.Schedules.Where(s => s.EndTime > from && s.StartTime <= to).OrderBy(s => s.StartTime)
                .Select(s => new DoctorScheduleDto { Id = s.Id, DoctorId = s.DoctorId, StartTime = s.StartTime, EndTime = s.EndTime, IsAvailable = s.IsAvailable }).ToList()
        }).AsSplitQuery().SingleOrDefaultAsync();
    }

    private static string SqlDate(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private sealed class DirectoryRow
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string LicenseNumber { get; set; } = "";
        public string Region { get; set; } = "";
        public string Bio { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public DateTime? NextAvailableSlot { get; set; }
        public int? ClinicId { get; set; }
        public string? ClinicName { get; set; }
        public string? ClinicAddress { get; set; }
        public double? ClinicLatitude { get; set; }
        public double? ClinicLongitude { get; set; }
        public double? DistanceKm { get; set; }
    }
}
