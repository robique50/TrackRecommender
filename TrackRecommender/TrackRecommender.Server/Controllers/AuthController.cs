﻿using Microsoft.AspNetCore.Mvc;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Services;

namespace TrackRecommender.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(AuthService authService) : ControllerBase
    {
        private readonly AuthService _authService = authService;

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var (Success, ErrorMessage) = await _authService.RegisterAsync(registerDto);

                if (!Success)
                    return BadRequest(new { message = ErrorMessage });

                return Ok(new { message = "Registration successful" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An unexpected error occurred", error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var ipAddress = GetIpAddress();
                var response = await _authService.AuthenticateAsync(loginDto, ipAddress);

                if (response == null)
                    return Unauthorized(new { message = "Username or password is incorrect" });

                SetRefreshTokenCookie(response.RefreshToken);

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An unexpected error occurred", error = ex.Message });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                if (string.IsNullOrEmpty(refreshToken))
                    return BadRequest(new { message = "Refresh token is required" });

                var ipAddress = GetIpAddress();
                var response = await _authService.RefreshTokenAsync(refreshToken, ipAddress);

                if (response == null)
                    return Unauthorized(new { message = "Invalid refresh token" });

                SetRefreshTokenCookie(response.RefreshToken);

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An unexpected error occurred", error = ex.Message });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return BadRequest(new { message = "No refresh token provided" });

            var ipAddress = GetIpAddress();
            var result = await _authService.RevokeTokenAsync(refreshToken, ipAddress);

            if (!result)
                return BadRequest(new { message = "Token is invalid or has already been revoked" });

            Response.Cookies.Delete("refreshToken");

            return Ok(new { message = "Token revoked successfully" });
        }

        private void SetRefreshTokenCookie(string token)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(7),
                SameSite = SameSiteMode.None,
                Secure = Request.IsHttps
            };
            Response.Cookies.Append("refreshToken", token, cookieOptions);
        }

        private string GetIpAddress()
        {
            if (Request.Headers.TryGetValue("X-Forwarded-For", out Microsoft.Extensions.Primitives.StringValues value))
            {
                var forwardedIp = value.ToString();
                if (!string.IsNullOrEmpty(forwardedIp))
                    return forwardedIp;
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress;

            if (ipAddress != null)
            {
                if (ipAddress.IsIPv4MappedToIPv6)
                    ipAddress = ipAddress.MapToIPv4();

                return ipAddress.ToString();
            }

            return "Unknown";
        }
    }
}