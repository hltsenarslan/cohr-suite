using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notification.Api.Infrastructure;
using Notification.Api.Models;
using Notification.Api.Services;

namespace Notification.Api.Endpoints;

public static class InternalMailEnpoints
{
    public static IEndpointRouteBuilder MapMailEndpoints(this IEndpointRouteBuilder app)
    {
        var mailApi = app.MapGroup("/api/notify/mail");

// ---- Admin: Tenant mail config upsert & get ----
        mailApi.MapPost("/settings/upsert", async (MailSetting dto, NotifDbContext db) =>
        {
            var existing = await db.MailSettings.FindAsync(dto.TenantId);
            if (existing is null)
            {
                dto.UpdatedAt = DateTime.UtcNow;
                db.MailSettings.Add(dto);
            }
            else
            {
                existing.Host = dto.Host;
                existing.Port = dto.Port;
                existing.UseSsl = dto.UseSsl;
                existing.Username = dto.Username;
                existing.Password = dto.Password;
                existing.FromName = dto.FromName;
                existing.FromEmail = dto.FromEmail;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        mailApi.MapGet("/settings/{tenantId:guid}", async (Guid tenantId, NotifDbContext db) =>
        {
            var s = await db.MailSettings.FindAsync(tenantId);
            return s is null ? Results.NotFound() : Results.Ok(s);
        });

// ---- Queue: enqueue mail ----

        mailApi.MapPost("/enqueue", async (EnqueueMailReq req, NotifDbContext db) =>
        {
            if (req.To == null || req.To.Length == 0) return Results.BadRequest(new { error = "to_required" });
            if (string.IsNullOrWhiteSpace(req.Subject)) return Results.BadRequest(new { error = "subject_required" });
            if (string.IsNullOrWhiteSpace(req.HtmlBody)) return Results.BadRequest(new { error = "body_required" });

            var item = new MailQueueItem
            {
                TenantId = req.TenantId,
                ToList = string.Join(",", req.To),
                Subject = req.Subject,
                HtmlBody = req.HtmlBody
            };

            if (req.AttachmentFileIds?.Length > 0)
            {
                foreach (var id in req.AttachmentFileIds.Take(5))
                    item.Attachments.Add(new MailAttachment { FileId = id });
            }

            db.MailQueue.Add(item);
            await db.SaveChangesAsync();

            return Results.Ok(new { id = item.Id, status = item.Status });
        });

// ---- Queue: peek/list (admin gözlem) ----
        mailApi.MapGet("/queue/{take}", async (int take, NotifDbContext db) =>
        {
            take = (take <= 0 || take > 100) ? 50 : take;
            var list = await db.MailQueue
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .Select(x => new
                {
                    x.Id, x.TenantId, x.ToList, x.Subject, x.Status, x.CreatedAt, x.StartedAt, x.FinishedAt, x.Error
                })
                .OrderByDescending(k => k.CreatedAt)
                .ToListAsync();
            return Results.Ok(list);
        });

// ---- Queue: trigger once (test için) ----
        mailApi.MapPost("/trigger", async ([FromServices] QueueWorker worker) =>
        {
            await worker.ProcessOnce(CancellationToken.None);
            return Results.Ok(new { triggered = true, at = DateTime.UtcNow });
        });
        return app;
    }
}