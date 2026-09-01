using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Models.Entities;

namespace SafePathBD.Web.Data;

public partial class SafePathDbContext : DbContext
{
    public SafePathDbContext(DbContextOptions<SafePathDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccidentReports> AccidentReports { get; set; }

    public virtual DbSet<AccidentSeverities> AccidentSeverities { get; set; }

    public virtual DbSet<AccidentTypes> AccidentTypes { get; set; }

    public virtual DbSet<Accidents> Accidents { get; set; }

    public virtual DbSet<AdminActions> AdminActions { get; set; }

    public virtual DbSet<EmergencyServiceTypes> EmergencyServiceTypes { get; set; }

    public virtual DbSet<EmergencyServices> EmergencyServices { get; set; }

    public virtual DbSet<Feedback> Feedback { get; set; }

    public virtual DbSet<HazardReports> HazardReports { get; set; }

    public virtual DbSet<HazardTypes> HazardTypes { get; set; }

    public virtual DbSet<Locations> Locations { get; set; }

    public virtual DbSet<NotificationTypes> NotificationTypes { get; set; }

    public virtual DbSet<Notifications> Notifications { get; set; }

    public virtual DbSet<ReportComments> ReportComments { get; set; }

    public virtual DbSet<ReportImages> ReportImages { get; set; }

    public virtual DbSet<ReportStatuses> ReportStatuses { get; set; }

    public virtual DbSet<ReportVerifications> ReportVerifications { get; set; }

    public virtual DbSet<ReportVotes> ReportVotes { get; set; }

    public virtual DbSet<Reports> Reports { get; set; }

    public virtual DbSet<RoadConditions> RoadConditions { get; set; }

    public virtual DbSet<RoadSegments> RoadSegments { get; set; }

    public virtual DbSet<Roads> Roads { get; set; }

    public virtual DbSet<Roles> Roles { get; set; }

    public virtual DbSet<RouteSegments> RouteSegments { get; set; }

    public virtual DbSet<Routes> Routes { get; set; }

    public virtual DbSet<SafetyScoreFactors> SafetyScoreFactors { get; set; }

    public virtual DbSet<SafetyScores> SafetyScores { get; set; }

    public virtual DbSet<SavedPlaces> SavedPlaces { get; set; }

    public virtual DbSet<SavedRoutes> SavedRoutes { get; set; }

    public virtual DbSet<SystemSettings> SystemSettings { get; set; }

    public virtual DbSet<TrafficConditions> TrafficConditions { get; set; }

    public virtual DbSet<UserLocationHistory> UserLocationHistory { get; set; }

    public virtual DbSet<UserRoles> UserRoles { get; set; }

    public virtual DbSet<Users> Users { get; set; }

    public virtual DbSet<VwEmergencyServicesWithLocation> VwEmergencyServicesWithLocation { get; set; }

    public virtual DbSet<VwLatestSegmentSafetyScore> VwLatestSegmentSafetyScore { get; set; }

    public virtual DbSet<VwReportOverview> VwReportOverview { get; set; }

    public virtual DbSet<WeatherConditions> WeatherConditions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<AccidentReports>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PRIMARY");

            entity.ToTable("accident_reports", tb => tb.HasComment("Accident-specific fields for reports whose report_type is ACCIDENT."));

            entity.HasIndex(e => e.SeverityId, "ix_accident_reports_severity");

            entity.HasIndex(e => e.AccidentTypeId, "ix_accident_reports_type");

            entity.Property(e => e.ReportId)
                .ValueGeneratedNever()
                .HasColumnName("report_id");
            entity.Property(e => e.AccidentOccurredAt)
                .HasColumnType("datetime")
                .HasColumnName("accident_occurred_at");
            entity.Property(e => e.AccidentTypeId).HasColumnName("accident_type_id");
            entity.Property(e => e.NumberOfDeaths).HasColumnName("number_of_deaths");
            entity.Property(e => e.NumberOfInjured).HasColumnName("number_of_injured");
            entity.Property(e => e.NumberOfVehicles).HasColumnName("number_of_vehicles");
            entity.Property(e => e.SeverityId).HasColumnName("severity_id");
            entity.Property(e => e.WeatherNotes)
                .HasMaxLength(255)
                .HasColumnName("weather_notes");

            entity.HasOne(d => d.AccidentType).WithMany(p => p.AccidentReports)
                .HasForeignKey(d => d.AccidentTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_accident_reports_type");

            entity.HasOne(d => d.Report).WithOne(p => p.AccidentReports)
                .HasForeignKey<AccidentReports>(d => d.ReportId)
                .HasConstraintName("fk_accident_reports_report");

            entity.HasOne(d => d.Severity).WithMany(p => p.AccidentReports)
                .HasForeignKey(d => d.SeverityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_accident_reports_severity");
        });

        modelBuilder.Entity<AccidentSeverities>(entity =>
        {
            entity.HasKey(e => e.SeverityId).HasName("PRIMARY");

            entity.ToTable("accident_severities", tb => tb.HasComment("Severity levels used to weight accident risk."));

            entity.HasIndex(e => e.SeverityName, "uq_accident_severities_name").IsUnique();

            entity.Property(e => e.SeverityId)
                .ValueGeneratedOnAdd()
                .HasColumnName("severity_id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.RiskWeight)
                .HasPrecision(5, 2)
                .HasColumnName("risk_weight");
            entity.Property(e => e.SeverityName)
                .HasMaxLength(50)
                .HasColumnName("severity_name");
        });

        modelBuilder.Entity<AccidentTypes>(entity =>
        {
            entity.HasKey(e => e.AccidentTypeId).HasName("PRIMARY");

            entity.ToTable("accident_types", tb => tb.HasComment("Types of road accidents."));

            entity.HasIndex(e => e.TypeName, "uq_accident_types_name").IsUnique();

            entity.Property(e => e.AccidentTypeId).HasColumnName("accident_type_id");
            entity.Property(e => e.DefaultRiskWeight)
                .HasPrecision(5, 2)
                .HasDefaultValueSql("'1.00'")
                .HasColumnName("default_risk_weight");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.TypeName)
                .HasMaxLength(100)
                .HasColumnName("type_name");
        });

        modelBuilder.Entity<Accidents>(entity =>
        {
            entity.HasKey(e => e.AccidentId).HasName("PRIMARY");

            entity.ToTable("accidents", tb => tb.HasComment("Verified accident history used for hotspot and road-risk calculations."));

            entity.HasIndex(e => e.VerifiedBy, "fk_accidents_verified_by");

            entity.HasIndex(e => new { e.LocationId, e.AccidentOccurredAt }, "ix_accidents_location_time");

            entity.HasIndex(e => new { e.RoadSegmentId, e.AccidentOccurredAt }, "ix_accidents_segment_time");

            entity.HasIndex(e => e.SeverityId, "ix_accidents_severity");

            entity.HasIndex(e => e.AccidentTypeId, "ix_accidents_type");

            entity.HasIndex(e => e.SourceReportId, "uq_accidents_source_report").IsUnique();

            entity.Property(e => e.AccidentId).HasColumnName("accident_id");
            entity.Property(e => e.AccidentOccurredAt)
                .HasColumnType("datetime")
                .HasColumnName("accident_occurred_at");
            entity.Property(e => e.AccidentTypeId).HasColumnName("accident_type_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.NumberOfDeaths).HasColumnName("number_of_deaths");
            entity.Property(e => e.NumberOfInjured).HasColumnName("number_of_injured");
            entity.Property(e => e.NumberOfVehicles).HasColumnName("number_of_vehicles");
            entity.Property(e => e.RoadSegmentId).HasColumnName("road_segment_id");
            entity.Property(e => e.SeverityId).HasColumnName("severity_id");
            entity.Property(e => e.SourceReportId).HasColumnName("source_report_id");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.VerifiedAt)
                .HasColumnType("datetime")
                .HasColumnName("verified_at");
            entity.Property(e => e.VerifiedBy).HasColumnName("verified_by");
            entity.Property(e => e.WeatherCondition)
                .HasMaxLength(100)
                .HasColumnName("weather_condition");

            entity.HasOne(d => d.AccidentType).WithMany(p => p.Accidents)
                .HasForeignKey(d => d.AccidentTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_accidents_type");

            entity.HasOne(d => d.Location).WithMany(p => p.Accidents)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_accidents_location");

            entity.HasOne(d => d.RoadSegment).WithMany(p => p.Accidents)
                .HasForeignKey(d => d.RoadSegmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_accidents_segment");

            entity.HasOne(d => d.Severity).WithMany(p => p.Accidents)
                .HasForeignKey(d => d.SeverityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_accidents_severity");

            entity.HasOne(d => d.SourceReport).WithOne(p => p.Accidents)
                .HasForeignKey<Accidents>(d => d.SourceReportId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_accidents_source_report");

            entity.HasOne(d => d.VerifiedByNavigation).WithMany(p => p.Accidents)
                .HasForeignKey(d => d.VerifiedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_accidents_verified_by");
        });

        modelBuilder.Entity<AdminActions>(entity =>
        {
            entity.HasKey(e => e.AdminActionId).HasName("PRIMARY");

            entity.ToTable("admin_actions", tb => tb.HasComment("Audit log for important administrator and moderator actions."));

            entity.HasIndex(e => new { e.AdminUserId, e.ActionAt }, "ix_admin_actions_admin_time");

            entity.HasIndex(e => new { e.EntityType, e.EntityId }, "ix_admin_actions_entity");

            entity.Property(e => e.AdminActionId).HasColumnName("admin_action_id");
            entity.Property(e => e.ActionAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("action_at");
            entity.Property(e => e.ActionType)
                .HasMaxLength(100)
                .HasColumnName("action_type");
            entity.Property(e => e.AdminUserId).HasColumnName("admin_user_id");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .HasColumnName("entity_type");
            entity.Property(e => e.MetadataJson)
                .HasColumnType("json")
                .HasColumnName("metadata_json");

            entity.HasOne(d => d.AdminUser).WithMany(p => p.AdminActions)
                .HasForeignKey(d => d.AdminUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_admin_actions_admin");
        });

        modelBuilder.Entity<EmergencyServiceTypes>(entity =>
        {
            entity.HasKey(e => e.ServiceTypeId).HasName("PRIMARY");

            entity.ToTable("emergency_service_types", tb => tb.HasComment("Hospital, Police Station, Fire Service, Ambulance and similar types."));

            entity.HasIndex(e => e.ServiceTypeName, "uq_emergency_service_types_name").IsUnique();

            entity.Property(e => e.ServiceTypeId).HasColumnName("service_type_id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.ServiceTypeName)
                .HasMaxLength(80)
                .HasColumnName("service_type_name");
        });

        modelBuilder.Entity<EmergencyServices>(entity =>
        {
            entity.HasKey(e => e.EmergencyServiceId).HasName("PRIMARY");

            entity.ToTable("emergency_services", tb => tb.HasComment("Emergency facilities displayed near a user, route or accident location."));

            entity.HasIndex(e => e.LocationId, "ix_emergency_services_location");

            entity.HasIndex(e => e.ServiceName, "ix_emergency_services_name");

            entity.HasIndex(e => e.ServiceTypeId, "ix_emergency_services_type");

            entity.Property(e => e.EmergencyServiceId).HasColumnName("emergency_service_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.EmergencyPhone)
                .HasMaxLength(30)
                .HasColumnName("emergency_phone");
            entity.Property(e => e.Is24Hours).HasColumnName("is_24_hours");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.IsVerified).HasColumnName("is_verified");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.OpeningHours)
                .HasMaxLength(255)
                .HasColumnName("opening_hours");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .HasColumnName("phone");
            entity.Property(e => e.ServiceName)
                .HasMaxLength(200)
                .HasColumnName("service_name");
            entity.Property(e => e.ServiceTypeId).HasColumnName("service_type_id");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.WebsiteUrl)
                .HasMaxLength(1000)
                .HasColumnName("website_url");

            entity.HasOne(d => d.Location).WithMany(p => p.EmergencyServices)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_emergency_services_location");

            entity.HasOne(d => d.ServiceType).WithMany(p => p.EmergencyServices)
                .HasForeignKey(d => d.ServiceTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_emergency_services_type");
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PRIMARY");

            entity.ToTable("feedback", tb => tb.HasComment("User feedback, suggestions and complaints."));

            entity.HasIndex(e => new { e.Status, e.SubmittedAt }, "ix_feedback_status_time");

            entity.HasIndex(e => e.UserId, "ix_feedback_user");

            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.Message)
                .HasColumnType("text")
                .HasColumnName("message");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.ResolvedAt)
                .HasColumnType("datetime")
                .HasColumnName("resolved_at");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'OPEN'")
                .HasColumnType("enum('OPEN','IN_REVIEW','RESOLVED','CLOSED')")
                .HasColumnName("status");
            entity.Property(e => e.Subject)
                .HasMaxLength(200)
                .HasColumnName("subject");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("submitted_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Feedback)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_feedback_user");
        });

        modelBuilder.Entity<HazardReports>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PRIMARY");

            entity.ToTable("hazard_reports", tb => tb.HasComment("Hazard-specific fields for reports whose report_type is HAZARD."));

            entity.HasIndex(e => e.RiskLevel, "ix_hazard_reports_risk");

            entity.HasIndex(e => e.HazardTypeId, "ix_hazard_reports_type");

            entity.Property(e => e.ReportId)
                .ValueGeneratedNever()
                .HasColumnName("report_id");
            entity.Property(e => e.ExpectedClearanceAt)
                .HasColumnType("datetime")
                .HasColumnName("expected_clearance_at");
            entity.Property(e => e.HazardTypeId).HasColumnName("hazard_type_id");
            entity.Property(e => e.ObservedAt)
                .HasColumnType("datetime")
                .HasColumnName("observed_at");
            entity.Property(e => e.RiskLevel)
                .HasDefaultValueSql("'MODERATE'")
                .HasColumnType("enum('LOW','MODERATE','HIGH','CRITICAL')")
                .HasColumnName("risk_level");

            entity.HasOne(d => d.HazardType).WithMany(p => p.HazardReports)
                .HasForeignKey(d => d.HazardTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_hazard_reports_type");

            entity.HasOne(d => d.Report).WithOne(p => p.HazardReports)
                .HasForeignKey<HazardReports>(d => d.ReportId)
                .HasConstraintName("fk_hazard_reports_report");
        });

        modelBuilder.Entity<HazardTypes>(entity =>
        {
            entity.HasKey(e => e.HazardTypeId).HasName("PRIMARY");

            entity.ToTable("hazard_types", tb => tb.HasComment("Road hazard categories such as pothole, waterlogging or broken signal."));

            entity.HasIndex(e => e.HazardName, "uq_hazard_types_name").IsUnique();

            entity.Property(e => e.HazardTypeId).HasColumnName("hazard_type_id");
            entity.Property(e => e.DefaultRiskWeight)
                .HasPrecision(5, 2)
                .HasDefaultValueSql("'1.00'")
                .HasColumnName("default_risk_weight");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.HazardName)
                .HasMaxLength(100)
                .HasColumnName("hazard_name");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
        });

        modelBuilder.Entity<Locations>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("PRIMARY");

            entity.ToTable("locations", tb => tb.HasComment("Reusable map locations used by reports, routes, roads and emergency services."));

            entity.HasIndex(e => new { e.City, e.AreaName }, "ix_locations_city_area");

            entity.HasIndex(e => new { e.PlaceProvider, e.ExternalPlaceId }, "ix_locations_external_place");

            entity.HasIndex(e => new { e.Latitude, e.Longitude }, "ix_locations_lat_lng");

            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.AddressLine)
                .HasMaxLength(500)
                .HasColumnName("address_line");
            entity.Property(e => e.AreaName)
                .HasMaxLength(150)
                .HasColumnName("area_name");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.Country)
                .HasMaxLength(100)
                .HasDefaultValueSql("'Bangladesh'")
                .HasColumnName("country");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.District)
                .HasMaxLength(100)
                .HasColumnName("district");
            entity.Property(e => e.DivisionName)
                .HasMaxLength(100)
                .HasColumnName("division_name");
            entity.Property(e => e.ExternalPlaceId).HasColumnName("external_place_id");
            entity.Property(e => e.LandmarkName)
                .HasMaxLength(200)
                .HasColumnName("landmark_name");
            entity.Property(e => e.Latitude)
                .HasPrecision(10, 7)
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasPrecision(10, 7)
                .HasColumnName("longitude");
            entity.Property(e => e.PlaceProvider)
                .HasDefaultValueSql("'MANUAL'")
                .HasColumnType("enum('GOOGLE','OSM','MANUAL','OTHER')")
                .HasColumnName("place_provider");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<NotificationTypes>(entity =>
        {
            entity.HasKey(e => e.NotificationTypeId).HasName("PRIMARY");

            entity.ToTable("notification_types", tb => tb.HasComment("Categories of notifications sent by the platform."));

            entity.HasIndex(e => e.TypeCode, "uq_notification_types_code").IsUnique();

            entity.HasIndex(e => e.TypeName, "uq_notification_types_name").IsUnique();

            entity.Property(e => e.NotificationTypeId).HasColumnName("notification_type_id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.TypeCode)
                .HasMaxLength(50)
                .HasColumnName("type_code");
            entity.Property(e => e.TypeName)
                .HasMaxLength(100)
                .HasColumnName("type_name");
        });

        modelBuilder.Entity<Notifications>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PRIMARY");

            entity.ToTable("notifications", tb => tb.HasComment("User-specific safety, report, route and system notifications."));

            entity.HasIndex(e => e.ReportId, "ix_notifications_report");

            entity.HasIndex(e => e.RouteId, "ix_notifications_route");

            entity.HasIndex(e => e.RoadSegmentId, "ix_notifications_segment");

            entity.HasIndex(e => e.NotificationTypeId, "ix_notifications_type");

            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt }, "ix_notifications_user_read_time");

            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead).HasColumnName("is_read");
            entity.Property(e => e.Message)
                .HasColumnType("text")
                .HasColumnName("message");
            entity.Property(e => e.NotificationTypeId).HasColumnName("notification_type_id");
            entity.Property(e => e.ReadAt)
                .HasColumnType("datetime")
                .HasColumnName("read_at");
            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.RoadSegmentId).HasColumnName("road_segment_id");
            entity.Property(e => e.RouteId).HasColumnName("route_id");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.NotificationType).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.NotificationTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_notifications_type");

            entity.HasOne(d => d.Report).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.ReportId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_notifications_report");

            entity.HasOne(d => d.RoadSegment).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.RoadSegmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_notifications_segment");

            entity.HasOne(d => d.Route).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.RouteId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_notifications_route");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_notifications_user");
        });

        modelBuilder.Entity<ReportComments>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PRIMARY");

            entity.ToTable("report_comments", tb => tb.HasComment("Discussion and clarification comments on reports."));

            entity.HasIndex(e => e.ParentCommentId, "ix_report_comments_parent");

            entity.HasIndex(e => new { e.ReportId, e.CreatedAt }, "ix_report_comments_report");

            entity.HasIndex(e => e.UserId, "ix_report_comments_user");

            entity.Property(e => e.CommentId).HasColumnName("comment_id");
            entity.Property(e => e.CommentText)
                .HasColumnType("text")
                .HasColumnName("comment_text");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.ParentCommentId).HasColumnName("parent_comment_id");
            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.ParentComment).WithMany(p => p.InverseParentComment)
                .HasForeignKey(d => d.ParentCommentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_report_comments_parent");

            entity.HasOne(d => d.Report).WithMany(p => p.ReportComments)
                .HasForeignKey(d => d.ReportId)
                .HasConstraintName("fk_report_comments_report");

            entity.HasOne(d => d.User).WithMany(p => p.ReportComments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_report_comments_user");
        });

        modelBuilder.Entity<ReportImages>(entity =>
        {
            entity.HasKey(e => e.ImageId).HasName("PRIMARY");

            entity.ToTable("report_images", tb => tb.HasComment("Images attached to any accident or hazard report."));

            entity.HasIndex(e => e.ReportId, "ix_report_images_report");

            entity.Property(e => e.ImageId).HasColumnName("image_id");
            entity.Property(e => e.Caption)
                .HasMaxLength(255)
                .HasColumnName("caption");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(1000)
                .HasColumnName("image_url");
            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Report).WithMany(p => p.ReportImages)
                .HasForeignKey(d => d.ReportId)
                .HasConstraintName("fk_report_images_report");
        });

        modelBuilder.Entity<ReportStatuses>(entity =>
        {
            entity.HasKey(e => e.StatusId).HasName("PRIMARY");

            entity.ToTable("report_statuses", tb => tb.HasComment("Lifecycle states for accident and hazard reports."));

            entity.HasIndex(e => e.StatusCode, "uq_report_statuses_code").IsUnique();

            entity.HasIndex(e => e.StatusName, "uq_report_statuses_name").IsUnique();

            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.IsClosedStatus).HasColumnName("is_closed_status");
            entity.Property(e => e.StatusCode)
                .HasMaxLength(40)
                .HasColumnName("status_code");
            entity.Property(e => e.StatusName)
                .HasMaxLength(80)
                .HasColumnName("status_name");
        });

        modelBuilder.Entity<ReportVerifications>(entity =>
        {
            entity.HasKey(e => e.VerificationId).HasName("PRIMARY");

            entity.ToTable("report_verifications", tb => tb.HasComment("Audit history of moderator/admin decisions on reports."));

            entity.HasIndex(e => e.AdminUserId, "ix_report_verifications_admin");

            entity.HasIndex(e => new { e.ReportId, e.VerifiedAt }, "ix_report_verifications_report_time");

            entity.HasIndex(e => e.StatusId, "ix_report_verifications_status");

            entity.Property(e => e.VerificationId).HasColumnName("verification_id");
            entity.Property(e => e.AdminComment)
                .HasColumnType("text")
                .HasColumnName("admin_comment");
            entity.Property(e => e.AdminUserId).HasColumnName("admin_user_id");
            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.VerifiedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("verified_at");

            entity.HasOne(d => d.AdminUser).WithMany(p => p.ReportVerifications)
                .HasForeignKey(d => d.AdminUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_report_verifications_admin");

            entity.HasOne(d => d.Report).WithMany(p => p.ReportVerifications)
                .HasForeignKey(d => d.ReportId)
                .HasConstraintName("fk_report_verifications_report");

            entity.HasOne(d => d.Status).WithMany(p => p.ReportVerifications)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_report_verifications_status");
        });

        modelBuilder.Entity<ReportVotes>(entity =>
        {
            entity.HasKey(e => e.VoteId).HasName("PRIMARY");

            entity.ToTable("report_votes", tb => tb.HasComment("Community confirmation or dispute votes for reports."));

            entity.HasIndex(e => e.UserId, "ix_report_votes_user");

            entity.HasIndex(e => new { e.ReportId, e.UserId }, "uq_report_votes_report_user").IsUnique();

            entity.Property(e => e.VoteId).HasColumnName("vote_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VoteType)
                .HasColumnType("enum('CONFIRM','DISPUTE')")
                .HasColumnName("vote_type");

            entity.HasOne(d => d.Report).WithMany(p => p.ReportVotes)
                .HasForeignKey(d => d.ReportId)
                .HasConstraintName("fk_report_votes_report");

            entity.HasOne(d => d.User).WithMany(p => p.ReportVotes)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_report_votes_user");
        });

        modelBuilder.Entity<Reports>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PRIMARY");

            entity.ToTable("reports", tb => tb.HasComment("Parent table for all user-submitted accident and hazard reports."));

            entity.HasIndex(e => e.LocationId, "ix_reports_location");

            entity.HasIndex(e => e.ReportedAt, "ix_reports_reported_at");

            entity.HasIndex(e => e.RoadSegmentId, "ix_reports_segment");

            entity.HasIndex(e => new { e.StatusId, e.ReportType }, "ix_reports_status_type");

            entity.HasIndex(e => e.UserId, "ix_reports_user");

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.IsPublic)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_public");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.ReportType)
                .HasColumnType("enum('ACCIDENT','HAZARD')")
                .HasColumnName("report_type");
            entity.Property(e => e.ReportedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("reported_at");
            entity.Property(e => e.ResolvedAt)
                .HasColumnType("datetime")
                .HasColumnName("resolved_at");
            entity.Property(e => e.RoadSegmentId).HasColumnName("road_segment_id");
            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Location).WithMany(p => p.Reports)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_reports_location");

            entity.HasOne(d => d.RoadSegment).WithMany(p => p.Reports)
                .HasForeignKey(d => d.RoadSegmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_reports_segment");

            entity.HasOne(d => d.Status).WithMany(p => p.Reports)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_reports_status");

            entity.HasOne(d => d.User).WithMany(p => p.Reports)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_reports_user");
        });

        modelBuilder.Entity<RoadConditions>(entity =>
        {
            entity.HasKey(e => e.RoadConditionId).HasName("PRIMARY");

            entity.ToTable("road_conditions", tb => tb.HasComment("Historical road quality observations for each segment."));

            entity.HasIndex(e => e.RecordedBy, "ix_road_conditions_recorded_by");

            entity.HasIndex(e => new { e.RoadSegmentId, e.RecordedAt }, "ix_road_conditions_segment_time");

            entity.Property(e => e.RoadConditionId).HasColumnName("road_condition_id");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.DrainageScore)
                .HasPrecision(5, 2)
                .HasColumnName("drainage_score");
            entity.Property(e => e.LightingScore)
                .HasPrecision(5, 2)
                .HasColumnName("lighting_score");
            entity.Property(e => e.OverallConditionScore)
                .HasPrecision(5, 2)
                .HasColumnName("overall_condition_score");
            entity.Property(e => e.RecordedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("recorded_at");
            entity.Property(e => e.RecordedBy).HasColumnName("recorded_by");
            entity.Property(e => e.RoadSegmentId).HasColumnName("road_segment_id");
            entity.Property(e => e.SourceType)
                .HasDefaultValueSql("'ADMIN'")
                .HasColumnType("enum('ADMIN','USER_REPORT','API','SURVEY','OTHER')")
                .HasColumnName("source_type");
            entity.Property(e => e.SurfaceCondition)
                .HasColumnType("enum('EXCELLENT','GOOD','MODERATE','POOR','DANGEROUS')")
                .HasColumnName("surface_condition");
            entity.Property(e => e.SurfaceScore)
                .HasPrecision(5, 2)
                .HasColumnName("surface_score");
            entity.Property(e => e.VisibilityScore)
                .HasPrecision(5, 2)
                .HasColumnName("visibility_score");

            entity.HasOne(d => d.RecordedByNavigation).WithMany(p => p.RoadConditions)
                .HasForeignKey(d => d.RecordedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_road_conditions_recorded_by");

            entity.HasOne(d => d.RoadSegment).WithMany(p => p.RoadConditions)
                .HasForeignKey(d => d.RoadSegmentId)
                .HasConstraintName("fk_road_conditions_segment");
        });

        modelBuilder.Entity<RoadSegments>(entity =>
        {
            entity.HasKey(e => e.RoadSegmentId).HasName("PRIMARY");

            entity.ToTable("road_segments", tb => tb.HasComment("Smaller road sections used for risk scoring and route calculation."));

            entity.HasIndex(e => e.EndLocationId, "ix_road_segments_end");

            entity.HasIndex(e => e.RoadId, "ix_road_segments_road");

            entity.HasIndex(e => e.StartLocationId, "ix_road_segments_start");

            entity.Property(e => e.RoadSegmentId).HasColumnName("road_segment_id");
            entity.Property(e => e.AverageTravelTimeMin)
                .HasPrecision(8, 2)
                .HasColumnName("average_travel_time_min");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.DistanceKm)
                .HasPrecision(10, 3)
                .HasColumnName("distance_km");
            entity.Property(e => e.EncodedPolyline).HasColumnName("encoded_polyline");
            entity.Property(e => e.EndLocationId).HasColumnName("end_location_id");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.IsOneWay).HasColumnName("is_one_way");
            entity.Property(e => e.LaneCount).HasColumnName("lane_count");
            entity.Property(e => e.RoadId).HasColumnName("road_id");
            entity.Property(e => e.SegmentName)
                .HasMaxLength(200)
                .HasColumnName("segment_name");
            entity.Property(e => e.SpeedLimitKmh).HasColumnName("speed_limit_kmh");
            entity.Property(e => e.StartLocationId).HasColumnName("start_location_id");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.EndLocation).WithMany(p => p.RoadSegmentsEndLocation)
                .HasForeignKey(d => d.EndLocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_road_segments_end_location");

            entity.HasOne(d => d.Road).WithMany(p => p.RoadSegments)
                .HasForeignKey(d => d.RoadId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_road_segments_road");

            entity.HasOne(d => d.StartLocation).WithMany(p => p.RoadSegmentsStartLocation)
                .HasForeignKey(d => d.StartLocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_road_segments_start_location");
        });

        modelBuilder.Entity<Roads>(entity =>
        {
            entity.HasKey(e => e.RoadId).HasName("PRIMARY");

            entity.ToTable("roads", tb => tb.HasComment("Master list of roads managed by the platform."));

            entity.HasIndex(e => new { e.RoadName, e.City }, "ix_roads_name_city");

            entity.HasIndex(e => e.RoadCode, "uq_roads_code").IsUnique();

            entity.Property(e => e.RoadId).HasColumnName("road_id");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.DefaultSpeedLimitKmh).HasColumnName("default_speed_limit_kmh");
            entity.Property(e => e.District)
                .HasMaxLength(100)
                .HasColumnName("district");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.RoadCode)
                .HasMaxLength(50)
                .HasColumnName("road_code");
            entity.Property(e => e.RoadName)
                .HasMaxLength(200)
                .HasColumnName("road_name");
            entity.Property(e => e.RoadType)
                .HasMaxLength(80)
                .HasColumnName("road_type");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Roles>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PRIMARY");

            entity.ToTable("roles", tb => tb.HasComment("Application roles such as Admin, Moderator and User."));

            entity.HasIndex(e => e.RoleName, "uq_roles_name").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<RouteSegments>(entity =>
        {
            entity.HasKey(e => e.RouteSegmentId).HasName("PRIMARY");

            entity.ToTable("route_segments", tb => tb.HasComment("Many-to-many bridge between routes and road segments, preserving segment order."));

            entity.HasIndex(e => e.RoadSegmentId, "ix_route_segments_road_segment");

            entity.HasIndex(e => new { e.RouteId, e.SequenceNo }, "uq_route_segments_sequence").IsUnique();

            entity.Property(e => e.RouteSegmentId).HasColumnName("route_segment_id");
            entity.Property(e => e.DistanceKm)
                .HasPrecision(10, 3)
                .HasColumnName("distance_km");
            entity.Property(e => e.EstimatedDurationMin)
                .HasPrecision(10, 2)
                .HasColumnName("estimated_duration_min");
            entity.Property(e => e.RoadSegmentId).HasColumnName("road_segment_id");
            entity.Property(e => e.RouteId).HasColumnName("route_id");
            entity.Property(e => e.SegmentSafetyScore)
                .HasPrecision(5, 2)
                .HasColumnName("segment_safety_score");
            entity.Property(e => e.SequenceNo).HasColumnName("sequence_no");

            entity.HasOne(d => d.RoadSegment).WithMany(p => p.RouteSegments)
                .HasForeignKey(d => d.RoadSegmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_route_segments_road_segment");

            entity.HasOne(d => d.Route).WithMany(p => p.RouteSegments)
                .HasForeignKey(d => d.RouteId)
                .HasConstraintName("fk_route_segments_route");
        });

        modelBuilder.Entity<Routes>(entity =>
        {
            entity.HasKey(e => e.RouteId).HasName("PRIMARY");

            entity.ToTable("routes", tb => tb.HasComment("Generated route alternatives such as safest, fastest and shortest."));

            entity.HasIndex(e => e.DestinationLocationId, "fk_routes_destination_location");

            entity.HasIndex(e => new { e.StartLocationId, e.DestinationLocationId }, "ix_routes_start_destination");

            entity.HasIndex(e => e.RouteType, "ix_routes_type");

            entity.HasIndex(e => new { e.UserId, e.GeneratedAt }, "ix_routes_user_time");

            entity.Property(e => e.RouteId).HasColumnName("route_id");
            entity.Property(e => e.DestinationLocationId).HasColumnName("destination_location_id");
            entity.Property(e => e.EncodedPolyline).HasColumnName("encoded_polyline");
            entity.Property(e => e.EstimatedDurationMin)
                .HasPrecision(10, 2)
                .HasColumnName("estimated_duration_min");
            entity.Property(e => e.ExpiresAt)
                .HasColumnType("datetime")
                .HasColumnName("expires_at");
            entity.Property(e => e.GeneratedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("generated_at");
            entity.Property(e => e.OverallSafetyScore)
                .HasPrecision(5, 2)
                .HasColumnName("overall_safety_score");
            entity.Property(e => e.RouteType)
                .HasColumnType("enum('SAFEST','FASTEST','SHORTEST')")
                .HasColumnName("route_type");
            entity.Property(e => e.StartLocationId).HasColumnName("start_location_id");
            entity.Property(e => e.TotalDistanceKm)
                .HasPrecision(10, 3)
                .HasColumnName("total_distance_km");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.DestinationLocation).WithMany(p => p.RoutesDestinationLocation)
                .HasForeignKey(d => d.DestinationLocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_routes_destination_location");

            entity.HasOne(d => d.StartLocation).WithMany(p => p.RoutesStartLocation)
                .HasForeignKey(d => d.StartLocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_routes_start_location");

            entity.HasOne(d => d.User).WithMany(p => p.Routes)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_routes_user");
        });

        modelBuilder.Entity<SafetyScoreFactors>(entity =>
        {
            entity.HasKey(e => e.FactorId).HasName("PRIMARY");

            entity.ToTable("safety_score_factors", tb => tb.HasComment("Explainable factor-by-factor breakdown behind each road safety score."));

            entity.HasIndex(e => new { e.SafetyScoreId, e.FactorType }, "uq_safety_score_factor_type").IsUnique();

            entity.Property(e => e.FactorId).HasColumnName("factor_id");
            entity.Property(e => e.Details)
                .HasMaxLength(500)
                .HasColumnName("details");
            entity.Property(e => e.FactorType)
                .HasColumnType("enum('ACCIDENT','HAZARD','ROAD_CONDITION','TRAFFIC','WEATHER','LIGHTING','OTHER')")
                .HasColumnName("factor_type");
            entity.Property(e => e.FactorWeight)
                .HasPrecision(6, 5)
                .HasColumnName("factor_weight");
            entity.Property(e => e.NormalizedRiskScore)
                .HasPrecision(5, 2)
                .HasColumnName("normalized_risk_score");
            entity.Property(e => e.RawValue)
                .HasPrecision(12, 4)
                .HasColumnName("raw_value");
            entity.Property(e => e.SafetyScoreId).HasColumnName("safety_score_id");
            entity.Property(e => e.WeightedRiskScore)
                .HasPrecision(7, 3)
                .HasColumnName("weighted_risk_score");

            entity.HasOne(d => d.SafetyScore).WithMany(p => p.SafetyScoreFactors)
                .HasForeignKey(d => d.SafetyScoreId)
                .HasConstraintName("fk_safety_score_factors_score");
        });

        modelBuilder.Entity<SafetyScores>(entity =>
        {
            entity.HasKey(e => e.SafetyScoreId).HasName("PRIMARY");

            entity.ToTable("safety_scores", tb => tb.HasComment("Calculated safety score snapshots for road segments. Higher score means safer."));

            entity.HasIndex(e => e.RiskLevel, "ix_safety_scores_risk");

            entity.HasIndex(e => new { e.RoadSegmentId, e.CalculatedAt }, "ix_safety_scores_segment_time");

            entity.Property(e => e.SafetyScoreId).HasColumnName("safety_score_id");
            entity.Property(e => e.CalculatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("calculated_at");
            entity.Property(e => e.MethodologyVersion)
                .HasMaxLength(30)
                .HasDefaultValueSql("'1.0'")
                .HasColumnName("methodology_version");
            entity.Property(e => e.OverallSafetyScore)
                .HasPrecision(5, 2)
                .HasColumnName("overall_safety_score");
            entity.Property(e => e.RiskLevel)
                .HasColumnType("enum('LOW','MODERATE','HIGH','CRITICAL')")
                .HasColumnName("risk_level");
            entity.Property(e => e.RoadSegmentId).HasColumnName("road_segment_id");
            entity.Property(e => e.ValidUntil)
                .HasColumnType("datetime")
                .HasColumnName("valid_until");

            entity.HasOne(d => d.RoadSegment).WithMany(p => p.SafetyScores)
                .HasForeignKey(d => d.RoadSegmentId)
                .HasConstraintName("fk_safety_scores_segment");
        });

        modelBuilder.Entity<SavedPlaces>(entity =>
        {
            entity.HasKey(e => e.SavedPlaceId).HasName("PRIMARY");

            entity.ToTable("saved_places", tb => tb.HasComment("Named locations saved by users, such as Home or University."));

            entity.HasIndex(e => e.LocationId, "ix_saved_places_location");

            entity.HasIndex(e => new { e.UserId, e.PlaceName }, "uq_saved_places_user_name").IsUnique();

            entity.Property(e => e.SavedPlaceId).HasColumnName("saved_place_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.PlaceName)
                .HasMaxLength(120)
                .HasColumnName("place_name");
            entity.Property(e => e.PlaceType)
                .HasDefaultValueSql("'OTHER'")
                .HasColumnType("enum('HOME','OFFICE','UNIVERSITY','FAVORITE','OTHER')")
                .HasColumnName("place_type");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Location).WithMany(p => p.SavedPlaces)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_saved_places_location");

            entity.HasOne(d => d.User).WithMany(p => p.SavedPlaces)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_saved_places_user");
        });

        modelBuilder.Entity<SavedRoutes>(entity =>
        {
            entity.HasKey(e => e.SavedRouteId).HasName("PRIMARY");

            entity.ToTable("saved_routes", tb => tb.HasComment("Routes explicitly bookmarked by users."));

            entity.HasIndex(e => e.RouteId, "ix_saved_routes_route");

            entity.HasIndex(e => new { e.UserId, e.CustomName }, "uq_saved_routes_user_name").IsUnique();

            entity.HasIndex(e => new { e.UserId, e.RouteId }, "uq_saved_routes_user_route").IsUnique();

            entity.Property(e => e.SavedRouteId).HasColumnName("saved_route_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomName)
                .HasMaxLength(150)
                .HasColumnName("custom_name");
            entity.Property(e => e.RouteId).HasColumnName("route_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Route).WithMany(p => p.SavedRoutes)
                .HasForeignKey(d => d.RouteId)
                .HasConstraintName("fk_saved_routes_route");

            entity.HasOne(d => d.User).WithMany(p => p.SavedRoutes)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_saved_routes_user");
        });

        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(e => e.SettingId).HasName("PRIMARY");

            entity.ToTable("system_settings", tb => tb.HasComment("Configurable application settings including safety-score weights."));

            entity.HasIndex(e => e.UpdatedBy, "ix_system_settings_updated_by");

            entity.HasIndex(e => e.SettingKey, "uq_system_settings_key").IsUnique();

            entity.Property(e => e.SettingId).HasColumnName("setting_id");
            entity.Property(e => e.DataType)
                .HasDefaultValueSql("'STRING'")
                .HasColumnType("enum('STRING','INTEGER','DECIMAL','BOOLEAN','JSON')")
                .HasColumnName("data_type");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.SettingKey)
                .HasMaxLength(120)
                .HasColumnName("setting_key");
            entity.Property(e => e.SettingValue)
                .HasColumnType("text")
                .HasColumnName("setting_value");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SystemSettings)
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_system_settings_updated_by");
        });

        modelBuilder.Entity<TrafficConditions>(entity =>
        {
            entity.HasKey(e => e.TrafficConditionId).HasName("PRIMARY");

            entity.ToTable("traffic_conditions", tb => tb.HasComment("Traffic snapshots used by fastest and safest route calculations."));

            entity.HasIndex(e => new { e.RoadSegmentId, e.RecordedAt }, "ix_traffic_conditions_segment_time");

            entity.Property(e => e.TrafficConditionId).HasColumnName("traffic_condition_id");
            entity.Property(e => e.AverageSpeedKmh)
                .HasPrecision(6, 2)
                .HasColumnName("average_speed_kmh");
            entity.Property(e => e.CongestionScore)
                .HasPrecision(5, 2)
                .HasColumnName("congestion_score");
            entity.Property(e => e.RecordedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("recorded_at");
            entity.Property(e => e.RoadSegmentId).HasColumnName("road_segment_id");
            entity.Property(e => e.Source)
                .HasMaxLength(100)
                .HasColumnName("source");
            entity.Property(e => e.TrafficLevel)
                .HasColumnType("enum('LOW','MODERATE','HEAVY','SEVERE')")
                .HasColumnName("traffic_level");

            entity.HasOne(d => d.RoadSegment).WithMany(p => p.TrafficConditions)
                .HasForeignKey(d => d.RoadSegmentId)
                .HasConstraintName("fk_traffic_conditions_segment");
        });

        modelBuilder.Entity<UserLocationHistory>(entity =>
        {
            entity.HasKey(e => e.UserLocationId).HasName("PRIMARY");

            entity.ToTable("user_location_history", tb => tb.HasComment("Optional, consent-based location captures for user actions; not continuous tracking."));

            entity.HasIndex(e => new { e.UserId, e.CapturedAt }, "ix_user_location_history_user_time");

            entity.Property(e => e.UserLocationId).HasColumnName("user_location_id");
            entity.Property(e => e.AccuracyMeters)
                .HasPrecision(10, 2)
                .HasColumnName("accuracy_meters");
            entity.Property(e => e.CapturedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("captured_at");
            entity.Property(e => e.Latitude)
                .HasPrecision(10, 7)
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasPrecision(10, 7)
                .HasColumnName("longitude");
            entity.Property(e => e.Purpose)
                .HasDefaultValueSql("'OTHER'")
                .HasColumnType("enum('ROUTE_REQUEST','ACCIDENT_REPORT','HAZARD_REPORT','EMERGENCY','OTHER')")
                .HasColumnName("purpose");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserLocationHistory)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_user_location_history_user");
        });

        modelBuilder.Entity<UserRoles>(entity =>
        {
            entity.HasKey(e => e.UserRoleId).HasName("PRIMARY");

            entity.ToTable("user_roles", tb => tb.HasComment("Many-to-many bridge between users and roles."));

            entity.HasIndex(e => e.RoleId, "ix_user_roles_role");

            entity.HasIndex(e => new { e.UserId, e.RoleId }, "uq_user_roles_user_role").IsUnique();

            entity.Property(e => e.UserRoleId).HasColumnName("user_role_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("assigned_at");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_user_roles_role");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_user_roles_user");
        });

        modelBuilder.Entity<Users>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.ToTable("users", tb => tb.HasComment("Registered SafePath BD users."));

            entity.HasIndex(e => e.Email, "uq_users_email").IsUnique();

            entity.HasIndex(e => e.Phone, "uq_users_phone").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(190)
                .HasColumnName("email");
            entity.Property(e => e.EmailVerified).HasColumnName("email_verified");
            entity.Property(e => e.FullName)
                .HasMaxLength(150)
                .HasColumnName("full_name");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.LastLoginAt)
                .HasColumnType("datetime")
                .HasColumnName("last_login_at");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .HasColumnName("phone");
            entity.Property(e => e.ProfileImageUrl)
                .HasMaxLength(1000)
                .HasColumnName("profile_image_url");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<VwEmergencyServicesWithLocation>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_emergency_services_with_location");

            entity.Property(e => e.AddressLine)
                .HasMaxLength(500)
                .HasColumnName("address_line");
            entity.Property(e => e.AreaName)
                .HasMaxLength(150)
                .HasColumnName("area_name");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.District)
                .HasMaxLength(100)
                .HasColumnName("district");
            entity.Property(e => e.EmergencyPhone)
                .HasMaxLength(30)
                .HasColumnName("emergency_phone");
            entity.Property(e => e.EmergencyServiceId).HasColumnName("emergency_service_id");
            entity.Property(e => e.Is24Hours).HasColumnName("is_24_hours");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.IsVerified).HasColumnName("is_verified");
            entity.Property(e => e.LandmarkName)
                .HasMaxLength(200)
                .HasColumnName("landmark_name");
            entity.Property(e => e.Latitude)
                .HasPrecision(10, 7)
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasPrecision(10, 7)
                .HasColumnName("longitude");
            entity.Property(e => e.OpeningHours)
                .HasMaxLength(255)
                .HasColumnName("opening_hours");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .HasColumnName("phone");
            entity.Property(e => e.ServiceName)
                .HasMaxLength(200)
                .HasColumnName("service_name");
            entity.Property(e => e.ServiceTypeName)
                .HasMaxLength(80)
                .HasColumnName("service_type_name");
            entity.Property(e => e.WebsiteUrl)
                .HasMaxLength(1000)
                .HasColumnName("website_url");
        });

        modelBuilder.Entity<VwLatestSegmentSafetyScore>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_latest_segment_safety_score");

            entity.Property(e => e.CalculatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("calculated_at");
            entity.Property(e => e.MethodologyVersion)
                .HasMaxLength(30)
                .HasDefaultValueSql("'1.0'")
                .HasColumnName("methodology_version");
            entity.Property(e => e.OverallSafetyScore)
                .HasPrecision(5, 2)
                .HasColumnName("overall_safety_score");
            entity.Property(e => e.RiskLevel)
                .HasColumnType("enum('LOW','MODERATE','HIGH','CRITICAL')")
                .HasColumnName("risk_level");
            entity.Property(e => e.RoadSegmentId).HasColumnName("road_segment_id");
            entity.Property(e => e.SafetyScoreId).HasColumnName("safety_score_id");
            entity.Property(e => e.ValidUntil)
                .HasColumnType("datetime")
                .HasColumnName("valid_until");
        });

        modelBuilder.Entity<VwReportOverview>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_report_overview");

            entity.Property(e => e.AreaName)
                .HasMaxLength(150)
                .HasColumnName("area_name");
            entity.Property(e => e.ConfirmVotes).HasColumnName("confirm_votes");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.DisputeVotes).HasColumnName("dispute_votes");
            entity.Property(e => e.LandmarkName)
                .HasMaxLength(200)
                .HasColumnName("landmark_name");
            entity.Property(e => e.Latitude)
                .HasPrecision(10, 7)
                .HasColumnName("latitude");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.Longitude)
                .HasPrecision(10, 7)
                .HasColumnName("longitude");
            entity.Property(e => e.ReportId)
                .HasDefaultValueSql("'0'")
                .HasColumnName("report_id");
            entity.Property(e => e.ReportType)
                .HasColumnType("enum('ACCIDENT','HAZARD')")
                .HasColumnName("report_type");
            entity.Property(e => e.ReportedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("reported_at");
            entity.Property(e => e.ReporterName)
                .HasMaxLength(150)
                .HasColumnName("reporter_name");
            entity.Property(e => e.ResolvedAt)
                .HasColumnType("datetime")
                .HasColumnName("resolved_at");
            entity.Property(e => e.RoadSegmentId).HasColumnName("road_segment_id");
            entity.Property(e => e.StatusCode)
                .HasMaxLength(40)
                .HasColumnName("status_code");
            entity.Property(e => e.StatusName)
                .HasMaxLength(80)
                .HasColumnName("status_name");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UserId).HasColumnName("user_id");
        });

        modelBuilder.Entity<WeatherConditions>(entity =>
        {
            entity.HasKey(e => e.WeatherConditionId).HasName("PRIMARY");

            entity.ToTable("weather_conditions", tb => tb.HasComment("Weather snapshots for weather-aware safety scoring."));

            entity.HasIndex(e => new { e.RoadSegmentId, e.RecordedAt }, "ix_weather_conditions_segment_time");

            entity.Property(e => e.WeatherConditionId).HasColumnName("weather_condition_id");
            entity.Property(e => e.RainfallMm)
                .HasPrecision(8, 2)
                .HasColumnName("rainfall_mm");
            entity.Property(e => e.RecordedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("recorded_at");
            entity.Property(e => e.RoadSegmentId).HasColumnName("road_segment_id");
            entity.Property(e => e.Source)
                .HasMaxLength(100)
                .HasColumnName("source");
            entity.Property(e => e.TemperatureC)
                .HasPrecision(5, 2)
                .HasColumnName("temperature_c");
            entity.Property(e => e.VisibilityMeters)
                .HasPrecision(10, 2)
                .HasColumnName("visibility_meters");
            entity.Property(e => e.WeatherRiskScore)
                .HasPrecision(5, 2)
                .HasColumnName("weather_risk_score");
            entity.Property(e => e.WeatherType)
                .HasMaxLength(80)
                .HasColumnName("weather_type");

            entity.HasOne(d => d.RoadSegment).WithMany(p => p.WeatherConditions)
                .HasForeignKey(d => d.RoadSegmentId)
                .HasConstraintName("fk_weather_conditions_segment");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
