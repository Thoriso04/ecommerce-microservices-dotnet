using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ecommerce.Security;
using UserService.Data;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/users")]
public class AuthController : ControllerBase
{
    private readonly UserDbContext _db;
    private readonly TokenService _tokenService;
    private readonly ILogger<AuthController> _logger;
    private readonly SecurityEventPublisher _eventPublisher;

    public AuthController(UserDbContext db, TokenService tokenService, ILogger<AuthController> logger, SecurityEventPublisher eventPublisher)
    {
        _db = db;
        _tokenService = tokenService;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
        {
            _logger.LogWarning("Registration failed - username {Username} already exists", req.Username);
            return Conflict(new { message = "Username already exists" });
        }

        var user = new User
        {
            Username = req.Username,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _eventPublisher.PublishAsync("USER_REGISTERED", "LOW", "user-service", $"New user registered: {user.Username}", userId: user.Username);

        _logger.LogInformation("New user registered: {Username} ({UserId})", user.Username, user.Id);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, new { user.Id, user.Username });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == req.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for username {Username}", req.Username);
            await _eventPublisher.PublishAsync("AUTH_FAILED", "MEDIUM", "user-service", $"Failed login attempt for username {req.Username}", userId: req.Username);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var token = _tokenService.GenerateToken(user);
        _logger.LogInformation("User {Username} logged in successfully", user.Username);
        await _eventPublisher.PublishAsync("AUTH_SUCCESS", "LOW", "user-service", $"User {user.Username} logged in successfully", userId: user.Username);
        return Ok(new AuthResponse(token, user.Id, user.Username, user.Role));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        return user == null ? NotFound() : Ok(new { user.Id, user.Username, user.Email, user.Role });
    }
}