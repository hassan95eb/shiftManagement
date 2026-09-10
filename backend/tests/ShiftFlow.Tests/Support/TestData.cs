using System;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Infrastructure.Persistence;

namespace ShiftFlow.Tests.Support;

/// <summary>Terse fixture builders for the Projects / Experts use-case tests.</summary>
public static class TestData
{
    private static readonly DateTime Seeded = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static Employer AddEmployer(this AppDbContext db, string name)
    {
        var employer = new Employer
        {
            Name = name,
            CreatedAtUtc = Seeded,
            User = new User
            {
                Username = name.ToLowerInvariant() + "-admin",
                PasswordHash = "x",
                Role = UserRole.Employer,
                IsActive = true,
                CreatedAtUtc = Seeded,
            },
        };
        db.Employers.Add(employer);
        db.SaveChanges();
        return employer;
    }

    public static Expert AddExpert(this AppDbContext db, string fullName)
    {
        var expert = new Expert
        {
            FullName = fullName,
            IsActive = true,
            CreatedAtUtc = Seeded,
            User = new User
            {
                Username = fullName.ToLowerInvariant().Replace(" ", "-"),
                PasswordHash = "x",
                Role = UserRole.Expert,
                IsActive = true,
                CreatedAtUtc = Seeded,
            },
        };
        db.Experts.Add(expert);
        db.SaveChanges();
        return expert;
    }

    public static Project AddProject(this AppDbContext db, int employerId, string name)
    {
        var project = new Project
        {
            EmployerId = employerId,
            Name = name,
            IsActive = true,
            CreatedAtUtc = Seeded,
        };
        db.Projects.Add(project);
        db.SaveChanges();
        return project;
    }

    public static Shift AddShift(this AppDbContext db, int projectId)
    {
        var shift = new Shift
        {
            ProjectId = projectId,
            StartUtc = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 7, 1, 16, 0, 0, DateTimeKind.Utc),
            Status = ShiftStatus.Open,
            CreatedAtUtc = Seeded,
        };
        db.Shifts.Add(shift);
        db.SaveChanges();
        return shift;
    }

    public static ExpertProject Assign(this AppDbContext db, int expertId, int projectId)
    {
        var link = new ExpertProject
        {
            ExpertId = expertId,
            ProjectId = projectId,
            AssignedAtUtc = Seeded,
        };
        db.ExpertProjects.Add(link);
        db.SaveChanges();
        return link;
    }

    public static ShiftApplication AddApplication(
        this AppDbContext db,
        int shiftId,
        int expertId,
        ApplicationStatus status)
    {
        var application = new ShiftApplication
        {
            ShiftId = shiftId,
            ExpertId = expertId,
            Status = status,
            AppliedAtUtc = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc),
        };
        db.ShiftApplications.Add(application);
        db.SaveChanges();
        return application;
    }
}
