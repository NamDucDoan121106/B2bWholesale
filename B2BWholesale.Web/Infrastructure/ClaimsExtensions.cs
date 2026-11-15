using System.Security.Claims;

namespace B2BWholesale.Web.Infrastructure;

public static class PortalClaims
{
    public const string AgentId = "AgentId";
    public const string AgentName = "AgentName";

    /// <summary>The agent the logged-in user belongs to. Always read from the auth cookie, never from the form.</summary>
    public static int GetAgentId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(AgentId), out var id)
            ? id
            : throw new UnauthorizedAccessException("The signed-in user is not linked to an agent.");

    public static string GetAgentName(this ClaimsPrincipal user) => user.FindFirstValue(AgentName) ?? user.Identity?.Name ?? "";
}
