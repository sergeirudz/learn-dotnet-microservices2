using Submission.API.Endpoints;

namespace Submission.Api.Endpoints;

public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        CreateArticleEndpoint.Map(app);
        AssignAuthorEndpoint.Map(app);
        CreateAndAssignAuthorEndpoint.Map(app);
        ApproveArticleEndpoint.Map(app);
        UploadManuscriptFileEndpoint.Map(app);

        return app;
    }
}