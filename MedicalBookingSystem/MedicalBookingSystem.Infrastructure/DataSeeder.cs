using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Infrastructure
{
    public class DataSeeder
    {
        private readonly ApplicationDbContext _dbContext;

        public DataSeeder(ApplicationDbContext context)
        {
            _dbContext = context;
        }

        public void Seed()
        {
            //_dbContext.Database.EnsureDeleted();
            //SeedAppointmentsTest();
            //SeedNotesTest();
            //SeedPrescriptionsTest();
            //SeedDoctorsTest();
            //SeedAppointmentsTest1();
            _dbContext.Database.EnsureCreated();
            if (!_dbContext.Doctors.Any())
            {
                SeedSpecializations();
                SeedDoctors();
                SeedPatients();
                SeedDoctorSpecializations();
                SeedDoctorSchedules();
                SeedAppointments();
                SeedReviews();
                SeedNotes();
                SeedPrescriptions();

                _dbContext.SaveChanges();
            }
        }

        private void SeedSpecializations()
        {
            var specializations = new List<Specialization>
        {
            new Specialization { Name = "Kardiolog", Description = "Choroby serca i układu krążenia" },
            new Specialization { Name = "Neurolog", Description = "Choroby układu nerwowego" },
            new Specialization { Name = "Pediatra", Description = "Lekarz dziecięcy" },
            new Specialization { Name = "Dermatolog", Description = "Choroby skóry" }
        };

            _dbContext.Specializations.AddRange(specializations);
            _dbContext.SaveChanges();
        }

        private void SeedDoctors()
        {
            var doctors = new List<Doctor>
        {
            new Doctor
            {
                FirstName = "Jan",
                LastName = "Kowalski",
                LicenseNumber = "DOC/12345",
                Region = "Mazowieckie",
                Bio = "Specjalista kardiolog z 10-letnim doświadczeniem",
                ImageUrl = "data:image/jpeg;base64,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"
            },
            new Doctor
            {
                FirstName = "Anna",
                LastName = "Nowak",
                LicenseNumber = "DOC/54321",
                Region = "Małopolskie",
                Bio = "Specjalista neurolog, członek Polskiego Towarzystwa Neurologicznego"
            },
            new Doctor
            {
                FirstName = "Piotr",
                LastName = "Wiśniewski",
                LicenseNumber = "DOC/67890",
                Region = "Dolnośląskie",
                Bio = "Pediatra z wieloletnim doświadczeniem w pracy z dziećmi"
            }
        };

            _dbContext.Doctors.AddRange(doctors);
            _dbContext.SaveChanges();
        }

        private void SeedPatients()
        {
            var patients = new List<Patient>
        {
            new Patient
            {
                FirstName = "Adam",
                LastName = "Nowak",
                Email = "adam.nowak@example.com",
                PhoneNumber = "123456789",
                PasswordHash = "hashed_password_1",
                DateOfBirth = new DateTime(1985, 5, 15),
            },
            new Patient
            {
                FirstName = "Ewa",
                LastName = "Kowalska",
                Email = "ewa.kowalska@example.com",
                PhoneNumber = "987654321",
                PasswordHash = "hashed_password_2",
                DateOfBirth = new DateTime(1990, 8, 22),
            }
        };

            _dbContext.Patients.AddRange(patients);
            _dbContext.SaveChanges();
        }

        private void SeedDoctorSpecializations()
        {
            var specializations = _dbContext.Specializations.ToList();
            var doctors = _dbContext.Doctors.ToList();

            var doctorSpecializations = new List<DoctorSpecialization>
        {
            new DoctorSpecialization { Doctor = doctors[0], Specialization = specializations[0] },
            new DoctorSpecialization { Doctor = doctors[0], Specialization = specializations[1] },
            new DoctorSpecialization { Doctor = doctors[1], Specialization = specializations[1] },
            new DoctorSpecialization { Doctor = doctors[2], Specialization = specializations[2] }
        };

            _dbContext.DoctorSpecializations.AddRange(doctorSpecializations);
            _dbContext.SaveChanges();
        }

        private void SeedDoctorSchedules()
        {
            var doctors = _dbContext.Doctors.ToList();

            var schedules = new List<DoctorSchedule>
        {
            new DoctorSchedule
            {
                Doctor = doctors[0],
                StartTime = new DateTime(2025, 6, 10, 8, 0, 0),
                EndTime = new DateTime(2025, 6, 10, 12, 0, 0),
                IsAvailable = true
            },
            new DoctorSchedule
            {
                Doctor = doctors[0],
                StartTime = new DateTime(2025, 6, 11, 13, 0, 0),
                EndTime = new DateTime(2025, 6, 11, 17, 0, 0),
                IsAvailable = true
            },
            new DoctorSchedule
            {
                Doctor = doctors[1],
                StartTime = new DateTime(2025, 6, 12, 9, 0, 0),
                EndTime = new DateTime(2025, 6, 12, 15, 0, 0),
                IsAvailable = true
            },
            new DoctorSchedule
            {
                Doctor = doctors[2],
                StartTime = new DateTime(2025, 6, 13, 10, 0, 0),
                EndTime = new DateTime(2025, 6, 13, 16, 0, 0),
                IsAvailable = true
            }
        };

            _dbContext.DoctorSchedules.AddRange(schedules);
            _dbContext.SaveChanges();
        }

        private void SeedAppointments()
        {
            var patients = _dbContext.Patients.ToList();
            var doctors = _dbContext.Doctors.ToList();
            var schedules = _dbContext.DoctorSchedules.ToList();

            var appointments = new List<Appointment>
        {
            new Appointment
            {
                Date = new DateTime(2025, 3, 10, 9, 0, 0),
                Patient = patients[1],
                Doctor = doctors[0],
                Status = AppointmentStatus.Completed
            },
            new Appointment
            {
                Date = new DateTime(2025, 8, 11, 14, 0, 0),
                Patient = patients[1],
                Doctor = doctors[0],
                Status = AppointmentStatus.Scheduled
            },
            new Appointment
            {
                Date = new DateTime(2025, 8, 12, 11, 0, 0),
                Patient = patients[1],
                Doctor = doctors[1],
                Status = AppointmentStatus.Scheduled
            }
        };

            _dbContext.Appointments.AddRange(appointments);
            _dbContext.SaveChanges();
        }

        private void SeedReviews()
        {
            var patients = _dbContext.Patients.ToList();
            var doctors = _dbContext.Doctors.ToList();

            var reviews = new List<Review>
        {
            new Review
            {
                Doctor = doctors[0],
                Patient = patients[0],
                Rating = 5,
                Comment = "Bardzo profesjonalna obsługa",
                CreatedDate = new DateTime(2025, 6, 5, 10, 30, 0)
            },
            new Review
            {
                Doctor = doctors[0],
                Patient = patients[1],
                Rating = 4,
                Comment = "Długi czas oczekiwania, ale lekarz bardzo kompetentny",
                CreatedDate = new DateTime(2025, 6, 5, 11, 15, 0)
            }
        };

            _dbContext.Reviews.AddRange(reviews);
            _dbContext.SaveChanges();
        }

        private void SeedNotes()
        {
            var appointments = _dbContext.Appointments.ToList();

            var notes = new List<Note>
        {
            new Note
            {
                Content = "Pacjent skarży się na bóle w klatce piersiowej. Zlecono EKG i morfologię.",
                Appointment = appointments[0],
                CreatedDate = new DateTime(2025, 6, 10, 10, 0, 0)
            }
        };

            _dbContext.Notes.AddRange(notes);
            _dbContext.SaveChanges();
        }

        private void SeedPrescriptions()
        {
            var appointments = _dbContext.Appointments.ToList();

            var prescriptions = new List<Prescription>
        {
            new Prescription
            {
                Medication = "Aspiryna",
                Dosage = "1 tabletka dziennie",
                Instructions = "Stosować po posiłku",
                CreatedDate = new DateTime(2025, 6, 10),
                ExpiryDate = new DateTime(2025, 9, 10),
                Appointment = appointments[0]
            }
        };

            _dbContext.Prescriptions.AddRange(prescriptions);
            _dbContext.SaveChanges();
        }

        private void SeedAppointmentsTest()
        {
            var patients = _dbContext.Patients.ToList();
            var doctors = _dbContext.Doctors.ToList();
            var schedules = _dbContext.DoctorSchedules.ToList();

            var appointments = new List<Appointment>
        {
            new Appointment
            {
                Date = new DateTime(2025, 3, 10, 9, 0, 0),
                Patient = patients[2],
                Doctor = doctors[0],
                Status = AppointmentStatus.Completed
            },
            new Appointment
            {
                Date = new DateTime(2025, 8, 11, 14, 0, 0),
                Patient = patients[2],
                Doctor = doctors[0],
                Status = AppointmentStatus.Scheduled
            },
            new Appointment
            {
                Date = new DateTime(2025, 8, 12, 11, 0, 0),
                Patient = patients[2],
                Doctor = doctors[1],
                Status = AppointmentStatus.Scheduled
            }
        };

            _dbContext.Appointments.AddRange(appointments);
            _dbContext.SaveChanges();
        }

        private void SeedNotesTest()
        {
            var appointments = _dbContext.Appointments.ToList();

            var notes = new List<Note>
        {
            new Note
            {
                Content = "Pacjent skarży się na bóle w klatce piersiowej. Zlecono EKG i morfologię.",
                Appointment = appointments[6],
                CreatedDate = new DateTime(2025, 6, 5, 10, 0, 0)
            },
            new Note
            {
                Content = "Pacjent przedawkował marsjanki.",
                Appointment = appointments[5],
                CreatedDate = new DateTime(2025, 6, 5, 10, 0, 0)
            }
        };

            _dbContext.Notes.AddRange(notes);
            _dbContext.SaveChanges();
        }

        private void SeedPrescriptionsTest()
        {
            var appointments = _dbContext.Appointments.ToList();

            var prescriptions = new List<Prescription>
        {
            new Prescription
            {
                Medication = "Aspiryna",
                Dosage = "1 tabletka dziennie",
                Instructions = "Stosować po posiłku",
                CreatedDate = new DateTime(2025, 6, 10),
                ExpiryDate = new DateTime(2025, 9, 10),
                Appointment = appointments[5]
            }
        };

            _dbContext.Prescriptions.AddRange(prescriptions);
            _dbContext.SaveChanges();
        }

        private void SeedDoctorsTest()
        {
            var doctors = new List<Doctor>
        {
            new Doctor
            {
                FirstName = "Józek",
                LastName = "Baczyński",
                LicenseNumber = "DOC/1222235",
                Region = "Lubelskie",
                Bio = "Specjalista kardiolog z 10-letnim doświadczeniem",
                ImageUrl = "data:image/jpeg;base64,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"
            }
        };

            _dbContext.Doctors.AddRange(doctors);
            _dbContext.SaveChanges();
        }

        private void SeedAppointmentsTest1()
        {
            var patients = _dbContext.Patients.ToList();
            var doctors = _dbContext.Doctors.ToList();
            var schedules = _dbContext.DoctorSchedules.ToList();

            var appointments = new List<Appointment>
        {
            new Appointment
            {
                Date = new DateTime(2025, 3, 10, 9, 0, 0),
                Patient = patients[2],
                Doctor = doctors[3],
                Status = AppointmentStatus.Completed
            },
            new Appointment
            {
                Date = new DateTime(2025, 2, 11, 14, 0, 0),
                Patient = patients[2],
                Doctor = doctors[3],
                Status = AppointmentStatus.Completed
            },
            new Appointment
            {
                Date = new DateTime(2025, 2, 12, 11, 0, 0),
                Patient = patients[2],
                Doctor = doctors[3],
                Status = AppointmentStatus.Completed
            }
        };

            _dbContext.Appointments.AddRange(appointments);
            _dbContext.SaveChanges();
        }
    }
}

