using System;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Infrastructure.Persistence;

namespace ShiftFlow.Tests.Support;

/// <summary>Terse fixture builders for the Projects / CallAgents use-case tests.</summary>
public static class TestData
{
    private static readonly DateTime Seeded = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static Supervisor AddSupervisor(this AppDbContext db, string name)
    {
        var supervisor = new Supervisor
        {
            Name = name,
            CreatedAtUtc = Seeded,
            User = new User
            {
                Username = name.ToLowerInvariant() + "-admin",
                PasswordHash = "x",
                Role = UserRole.Supervisor,
                IsActive = true,
                CreatedAtUtc = Seeded,
            },
        };
        db.Supervisors.Add(supervisor);
        db.SaveChanges();
        return supervisor;
    }

    public static CallAgent AddCallAgent(this AppDbContext db, string fullName)
    {
        var callAgent = new CallAgent
        {
            FullName = fullName,
            IsActive = true,
            CreatedAtUtc = Seeded,
            User = new User
            {
                Username = fullName.ToLowerInvariant().Replace(" ", "-"),
                PasswordHash = "x",
                Role = UserRole.CallAgent,
                IsActive = true,
                CreatedAtUtc = Seeded,
            },
        };
        db.CallAgents.Add(callAgent);
        db.SaveChanges();
        return callAgent;
    }

    public static Project AddProject(this AppDbContext db, int supervisorId, string name)
    {
        var project = new Project
        {
            SupervisorId = supervisorId,
            Name = name,
            IsActive = true,
            CreatedAtUtc = Seeded,
        };
        db.Projects.Add(project);
        db.SaveChanges();
        return project;
    }

    public static Shift AddShift(this AppDbContext db, int projectId) =>
        db.AddShift(
            projectId,
            new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 16, 0, 0, DateTimeKind.Utc));

    public static Shift AddShift(this AppDbContext db, int projectId, DateTime startUtc, DateTime endUtc) =>
        db.AddShift(projectId, startUtc, endUtc, ShiftStatus.Open);

    public static Shift AddShift(
        this AppDbContext db,
        int projectId,
        DateTime startUtc,
        DateTime endUtc,
        ShiftStatus status) =>
        db.AddShift(projectId, startUtc, endUtc, status, assignedCallAgentId: null);

    public static Shift AddShift(
        this AppDbContext db,
        int projectId,
        DateTime startUtc,
        DateTime endUtc,
        ShiftStatus status,
        int? assignedCallAgentId)
    {
        var shift = new Shift
        {
            ProjectId = projectId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Status = status,
            AssignedCallAgentId = assignedCallAgentId,
            CreatedAtUtc = Seeded,
        };
        db.Shifts.Add(shift);
        db.SaveChanges();
        return shift;
    }

    public static Availability AddAvailability(
        this AppDbContext db,
        int callAgentId,
        DateTime startUtc,
        DateTime endUtc)
    {
        var window = new Availability
        {
            CallAgentId = callAgentId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            CreatedAtUtc = Seeded,
        };
        db.Availabilities.Add(window);
        db.SaveChanges();
        return window;
    }

    public static CallAgentProject Assign(this AppDbContext db, int callAgentId, int projectId)
    {
        var link = new CallAgentProject
        {
            CallAgentId = callAgentId,
            ProjectId = projectId,
            AssignedAtUtc = Seeded,
        };
        db.CallAgentProjects.Add(link);
        db.SaveChanges();
        return link;
    }

    public static ShiftApplication AddApplication(
        this AppDbContext db,
        int shiftId,
        int callAgentId,
        ApplicationStatus status,
        DateTime? appliedAtUtc = null)
    {
        var application = new ShiftApplication
        {
            ShiftId = shiftId,
            CallAgentId = callAgentId,
            Status = status,
            AppliedAtUtc = appliedAtUtc ?? new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc),
        };
        db.ShiftApplications.Add(application);
        db.SaveChanges();
        return application;
    }

    public static Recommendation AddRecommendation(
        this AppDbContext db,
        int shiftId,
        int callAgentId,
        decimal score,
        string? reason = null,
        DateTime? computedAtUtc = null)
    {
        var recommendation = new Recommendation
        {
            ShiftId = shiftId,
            CallAgentId = callAgentId,
            Score = score,
            Reason = reason ?? $"Total {score}",
            ComputedAtUtc = computedAtUtc ?? new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
        };
        db.Recommendations.Add(recommendation);
        db.SaveChanges();
        return recommendation;
    }
}
