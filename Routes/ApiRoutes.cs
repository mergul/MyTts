

using MyTts.Controllers;
using MyTts.Models;

namespace MyTts.Routes
{
    public static class ApiRoutes
    {
        public static void RegisterMp3Routes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("api/mp3/feed/{language}", async context =>
            {
                var controller = context.RequestServices.GetService<Mp3Controller>();
                var language = context.Request.RouteValues["language"]?.ToString();
                var limit = int.TryParse(context.Request.Query["limit"], out var parsedLimit) ? parsedLimit : 20;
                await controller.Feed(language, limit);
            }).WithMetadata(new { Name = "elevenlabs.mp3.feed" });

            endpoints.MapPost("api/mp3/one", async context =>
            {
                var controller = context.RequestServices.GetService<Mp3Controller>();
                var oneRequest = new OneRequest
                {
                    // Map necessary properties from context to OneRequest
                    // Example: PropertyName = context.Request.Query["PropertyName"]
                };
                await controller.One(oneRequest);
            }).WithMetadata(new { Name = "elevenlabs.mp3.one" });

            endpoints.MapPost("api/mp3/list", async context =>
            {
                var controller = context.RequestServices.GetService<Mp3Controller>();
                var listRequest = new ListRequest
                {
                    // Map necessary properties from context to ListRequest
                    // Example: PropertyName = context.Request.Query["PropertyName"]
                };
                await controller.List(listRequest);
            }).WithMetadata(new { Name = "elevenlabs.mp3.list" });

            endpoints.MapGet("api/mp3/one/{id}", async context =>
            {
                var controller = context.RequestServices.GetService<Mp3Controller>();
                var id = context.Request.RouteValues["id"]?.ToString();
                await controller.GetFile(id);
            }).WithMetadata(new { Name = "elevenlabs.mp3.getone" });

            endpoints.MapGet("api/mp3/last/{language}", async context =>
            {
                var controller = context.RequestServices.GetService<Mp3Controller>();
                var language = context.Request.RouteValues["language"]?.ToString();
                await controller.GetLast(language);
            }).WithMetadata(new { Name = "elevenlabs.mp3.getlast" });
        }
    }
}