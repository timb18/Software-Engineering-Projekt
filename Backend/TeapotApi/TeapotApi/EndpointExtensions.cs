namespace TeapotApi;

public static class EndpointExtensions
{
    extension(IEndpointRouteBuilder app)
    {
        public IEndpointRouteBuilder MapGroups()
        {
            app.MapGroup("planner").MapPlannerGroup();
            return app;
        }
    }

    extension(RouteGroupBuilder builder)
    {
        private RouteGroupBuilder MapPlannerGroup()
        {
            builder.MapPost("generate", (CancellationToken cancellationToken) => {});
            
            return builder;
        }
    }
}