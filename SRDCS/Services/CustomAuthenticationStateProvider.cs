// Services/CustomAuthenticationStateProvider.cs
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Claims;

namespace SRDCS.Services
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ProtectedSessionStorage _sessionStorage;

        public CustomAuthenticationStateProvider(
            IHttpContextAccessor httpContextAccessor,
            ProtectedSessionStorage sessionStorage)
        {
            _httpContextAccessor = httpContextAccessor;
            _sessionStorage = sessionStorage;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // Try to get user from HttpContext first (for server-side rendering)
                var httpContext = _httpContextAccessor.HttpContext;

                if (httpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    // User is authenticated via cookie authentication
                    return new AuthenticationState(httpContext.User);
                }

                // Fallback: Try to get user from session storage
                var userSession = await _sessionStorage.GetAsync<UserSession>("UserSession");

                if (userSession.Success && userSession.Value != null)
                {
                    // Create claims from session data
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, userSession.Value.Username),
                        new Claim(ClaimTypes.NameIdentifier, userSession.Value.UserId.ToString()),
                        new Claim("UserType", userSession.Value.UserType),
                        new Claim("SACCOId", userSession.Value.SACCOId?.ToString() ?? "0"),
                        new Claim("SACCOName", userSession.Value.SACCOName ?? ""),
                        new Claim(ClaimTypes.Role, userSession.Value.UserType)
                    };

                    var identity = new ClaimsIdentity(claims, "CustomAuth");
                    var user = new ClaimsPrincipal(identity);

                    return new AuthenticationState(user);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                Console.WriteLine($"Error in GetAuthenticationStateAsync: {ex.Message}");
            }

            // Return anonymous user
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        public async Task UpdateAuthenticationStateAsync(UserSession? userSession)
        {
            ClaimsPrincipal claimsPrincipal;

            if (userSession != null)
            {
                // Save user session to storage
                await _sessionStorage.SetAsync("UserSession", userSession);

                // Create claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, userSession.Username),
                    new Claim(ClaimTypes.NameIdentifier, userSession.UserId.ToString()),
                    new Claim("UserType", userSession.UserType),
                    new Claim("SACCOId", userSession.SACCOId?.ToString() ?? "0"),
                    new Claim("SACCOName", userSession.SACCOName ?? ""),
                    new Claim(ClaimTypes.Role, userSession.UserType)
                };

                var identity = new ClaimsIdentity(claims, "CustomAuth");
                claimsPrincipal = new ClaimsPrincipal(identity);
            }
            else
            {
                // Clear session
                await _sessionStorage.DeleteAsync("UserSession");
                claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
            }

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
        }

        public async Task MarkUserAsAuthenticated(string username, string userType, int userId, int? saccoId, string saccoName)
        {
            var userSession = new UserSession
            {
                UserId = userId,
                Username = username,
                UserType = userType,
                SACCOId = saccoId,
                SACCOName = saccoName
            };

            await UpdateAuthenticationStateAsync(userSession);
        }

        public async Task MarkUserAsLoggedOut()
        {
            await UpdateAuthenticationStateAsync(null);
        }
    }

    public class UserSession
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public int? SACCOId { get; set; }
        public string? SACCOName { get; set; }
    }
}