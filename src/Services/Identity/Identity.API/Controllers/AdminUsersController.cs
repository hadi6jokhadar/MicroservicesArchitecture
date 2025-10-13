// using Identity.Application.Commands;
// using IhsanDev.Shared.Kernel.Enums.Identity;
// using MediatR;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;

// namespace Identity.API.Controllers;

// [ApiController]
// [Route("api/admin/[controller]")]
// [Authorize(Roles = "Admin,SuperAdmin")]
// public class AdminUsersController : ControllerBase
// {
//     private readonly IMediator _mediator;

//     public AdminUsersController(IMediator mediator)
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
//     /// Get all users with pagination and filtering
//     /// </summary>
//     [HttpGet]
//     public async Task<IActionResult> GetUsers(
//         [FromQuery] int pageNumber = 1,
//         [FromQuery] int pageSize = 10,
//         [FromQuery] string? searchTerm = null,
//         [FromQuery] string? role = null,
//         [FromQuery] bool? status = null)
//     {
//         UserRole? userRole = null;
//         if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, out var parsedRole))
//         {
//             userRole = parsedRole;
//         }

//         var query = new GetUsersCommand(pageNumber, pageSize, searchTerm, userRole, status);
//         var result = await _mediator.Send(query);

//         return Ok(result);
//     }

//     /// <summary>
//     /// Get user by ID
//     /// </summary>
//     [HttpGet("{id:int}")]
//     public async Task<IActionResult> GetUserById(int id)
//     {
//         var query = new GetUserByIdCommand(id);
//         var result = await _mediator.Send(query);

//         return Ok(result);
//     }

//     /// <summary>
//     /// Create a new user
//     /// </summary>
//     [HttpPost]
//     public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command)
//     {
//         var result = await _mediator.Send(command);

//         return CreatedAtAction(nameof(GetUserById), new { id = result!.Id }, result);
//     }

//     /// <summary>
//     /// Update an existing user
//     /// </summary>
//     [HttpPut]
//     public async Task<IActionResult> UpdateUser([FromBody] UpdateUserCommand command)
//     {
//         var result = await _mediator.Send(command);

//         return Ok(result);
//     }

//     /// <summary>
//     /// Delete a user (soft delete)
//     /// </summary>
//     [HttpDelete("{id:int}")]
//     public async Task<IActionResult> DeleteUser(int id)
//     {
//         var command = new DeleteUserCommand(id);
//         var result = await _mediator.Send(command);

//         return Ok(result);
//     }

//     /// <summary>
//     /// Toggle user status (enable/disable)
//     /// </summary>
//     [HttpPatch("{id:int}/toggle-status")]
//     public async Task<IActionResult> ToggleUserStatus(int id)
//     {
//         var command = new ToggleUserStatusCommand(id);
//         var result = await _mediator.Send(command);

//         return Ok(result);
//     }
// }