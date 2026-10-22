using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Claims;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Database;

public sealed class OperationLifetimeTests
{
    [Fact]
    public async Task ReusedCircuitService_SeesChangesSavedByAnotherOperation()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var circuit = scenario.Factory.Services.CreateScope();
        var service = await CreateServiceAsync(circuit.ServiceProvider, scenario.PatientId);
        Assert.Equal("First", (await service.GetPatientProfileAsync(scenario.PatientId)).Value.FirstName);
        using (var otherRequest = scenario.Factory.Services.CreateScope())
        {
            var db = otherRequest.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Patients.FindAsync(scenario.PatientId))!.FirstName = "Changed outside the circuit";
            await db.SaveChangesAsync();
        }
        Assert.Equal("Changed outside the circuit", (await service.GetPatientProfileAsync(scenario.PatientId)).Value.FirstName);
    }

    [Fact]
    public async Task ReusedService_ConcurrentReadsUseIndependentContexts()
    {
        using var database = new TemporarySqliteDatabase();
        var gate = new ReadGate();
        using var scenario = await SecurityScenario.CreateAsync(new MedicalApiFactory(database.ConnectionString, interceptor: gate));
        using var circuit = scenario.Factory.Services.CreateScope();
        var service = await CreateServiceAsync(circuit.ServiceProvider, scenario.PatientId);
        gate.Active = true;
        var results = await Task.WhenAll(service.GetPatientProfileAsync(scenario.PatientId), service.GetPatientProfileAsync(scenario.PatientId));
        Assert.All(results, r => Assert.True(r.IsSuccess));
        // Each read validates the session and then opens its own operation context.
        Assert.Equal(4, gate.Contexts.Distinct().Count());
    }

    private static async Task<PatientService> CreateServiceAsync(IServiceProvider services, int patientId)
    {
        var principal = await ScenarioPrincipal.PatientAsync(services, patientId);
        return ActivatorUtilities.CreateInstance<PatientService>(services, ScenarioPrincipal.Access(services, principal));
    }

    private sealed class ReadGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentBag<Guid> Contexts { get; } = [];
        public bool Active { get; set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Active && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("FROM \"Patients\""))
            {
                Contexts.Add(data.Context!.ContextId.InstanceId);
                if (Contexts.Count >= 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
