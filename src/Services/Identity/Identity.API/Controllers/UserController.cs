// using Identity.Application.Commands;
// using MediatR;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using System.Security.Claims;

// namespace Identity.API.Controllers;

// [ApiController]
// [Route("api/[controller]")]
// public class UserController : ControllerBase
// {
//     private readonly IMediator _mediator;

//     public UserController(IMediator mediator)
//     {
//         _mediator = mediator;
//     }

//     /// <summary>
//     /// User login
//     /// </summary>
//     [HttpPost("login")]
//     public async Task<IActionResult> Login([FromBody] LoginCommand command)
//     {
//         var result = await _mediator.Send(command);

//         return Ok(result);
//     }
    
//     /// <summary>
//     /// Register a new user account
//     /// </summary>
//     [HttpPost("register")]
//     public async Task<IActionResult> Register([FromBody] RegisterCommand command)
//     {
//         var result = await _mediator.Send(command);

//         return Created("", result);
//     }

//     /// <summary>
//     /// Refresh access token
//     /// </summary>
//     [HttpPost("refresh-token")]
//     public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command)
//     {
//         var result = await _mediator.Send(command);

//         return Ok(result);
//     }

//     /// <summary>
//     /// Logout current user (revoke refresh token)
//     /// </summary>
//     [HttpPost("logout")]
//     [Authorize]
//     public IActionResult Logout()
//     {
//         if (GetCurrentUserId() != 0)
//         {
//             // Revoke refresh token by setting it to null
//             // In a real application, you might want to create a separate service method for this
//             // For now, we'll just return success as the token will expire naturally
//         }

//         return Ok(new { message = "Logged out successfully" });
//     }  

//     /// <summary>
//     /// Request password reset
//     /// </summary>
//     [HttpPost("forgot-password")]
//     public async Task<IActionResult> ForgotPassword([FromBody] ForgetPasswordCommand command)
//     {
//         var result = await _mediator.Send(command);

//         return Ok(result);
//     }


//     /// <summary>
//     /// Get current user profile
//     /// </summary>
//     [HttpGet("profile")]
//     [Authorize]
//     public async Task<IActionResult> GetProfile()
//     {
//         var userId = GetCurrentUserId();
//         var query = new GetUserProfileCommand(userId);
//         var result = await _mediator.Send(query);

//         return Ok(result);
//     }

//     /// <summary>
//     /// Update current user profile
//     /// </summary>
//     [HttpPut("profile")]
//     [Authorize]
//     public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileCommand bodyCommand)
//     {
//         var userId = GetCurrentUserId();
//         var command = new UpdateProfileCommand(
//             userId,
//             bodyCommand.FirstName,
//             bodyCommand.LastName,
//             bodyCommand.PhoneNumber,
//             bodyCommand.ProfilePictureUrl
//         );

//         var result = await _mediator.Send(command);

//         return Ok(result);
//     }

//     /// <summary>
//     /// Forget password - send reset link
//     /// </summary>
//     [HttpPost("forget-password")]
//     public async Task<IActionResult> ForgetPassword([FromBody] ForgetPasswordCommand command)
//     {
//         var result = await _mediator.Send(command);

//         return Ok(result);
//     }

//     /// <summary>
//     /// Delete current user account
//     /// </summary>
//     [HttpDelete("me")]
//     [Authorize]
//     public async Task<IActionResult> DeleteMe()
//     {
//         var userId = GetCurrentUserId();
//         var command = new DeleteUserCommand(userId);
//         var result = await _mediator.Send(command);

//         return Ok(result);
//     }

//     private int GetCurrentUserId()
//     {
//         var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//         return int.TryParse(userIdClaim, out var userId) ? userId : 0;
//     }
// }