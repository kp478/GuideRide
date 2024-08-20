﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GuideRide.Data;
using GuideRide.Models;
using GuideRide.Models.GuideRide.Dtos;
using System.Security.Claims;
using System.Threading.Tasks;
using System;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly GuideRideContext _context;
    private readonly AuthService _authService;

    public UserController(GuideRideContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = new User
        {
            Name = registerDto.Name,
            PasswordHash = registerDto.Password, 
            Email = registerDto.Email,
            Address = registerDto.Address,
            DateOfBirth = registerDto.DateOfBirth

        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Registration successful" });
    }

    
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginDto loginDto)
    {
        var user = _context.Users
            .FirstOrDefault(u => u.Email == loginDto.Email && u.PasswordHash == loginDto.Password); 

        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var token = _authService.GenerateToken(user);

        return Ok(new { Token = token });
    }
    
    [HttpGet("users")]
    [Authorize(Roles = "Admin")] 
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _context.Users.ToListAsync();

        if (users == null || !users.Any())
        {
            return NotFound(new { message = "No users found" });
        }

        return Ok(users);
    }

   
    [HttpGet("profile")]
    [Authorize]
    public IActionResult GetProfile()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)); 

        var user = _context.Users.Find(userId);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

   
    [HttpPut("update")]
    [Authorize]
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserDto updateUserDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

       
        user.PasswordHash = updateUserDto.Password; 
        user.Name = updateUserDto.Name;
        user.Email = updateUserDto.Email;
        user.Address = updateUserDto.Address;
        user.DateOfBirth = updateUserDto.DateOfBirth;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User updated successfully" });
    }

   
    [HttpDelete("delete")]
    [Authorize]
    public async Task<IActionResult> DeleteUser()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User deleted successfully" });
    }

    // **Booking Operations**

    [HttpGet("available-cars")]
    [Authorize]
    public async Task<IActionResult> GetAvailableCars()
    {
        var availableCars = await _context.Cars
            .Where(c => c.Status)
            .ToListAsync();

        return Ok(availableCars);
    }

    
    [HttpGet("available-guides")]
    [Authorize]
    public async Task<IActionResult> GetAvailableGuides()
    {
        var availableGuides = await _context.Guides
            .Where(g => g.Status)
            .ToListAsync();

        return Ok(availableGuides);
    }

    [HttpPost("bookings")]
    [Authorize]
    public async Task<IActionResult> CreateBooking([FromBody] BookingDto bookingDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        
        var car = await _context.Cars.FirstOrDefaultAsync(c => c.Id == bookingDto.CarId && c.Status);
        var guide = await _context.Guides.FirstOrDefaultAsync(g => g.Id == bookingDto.GuideId && g.Status);

        if (car == null || guide == null)
        {
            return BadRequest(new { message = "Selected car or guide is not available" });
        }

   
        var numberOfDays = (bookingDto.EndDate - bookingDto.StartDate).Days;
        var carFare = car.Fare;
        var guideFare = guide.Fare;
        var totalAmount = (carFare + guideFare) * numberOfDays + 200; 
        
        var booking = new Booking
        {
            CustomerId = userId,
            CarId = car.Id,
            GuideId = guide.Id,
            StartDate = bookingDto.StartDate,
            EndDate = bookingDto.EndDate,
            NumberOfDays = numberOfDays,
            TotalAmount = totalAmount,
            Bording = bookingDto.Bording, 
            Destination = bookingDto.Destination 
        };

        _context.Bookings.Add(booking);

        
        car.Status = false;
        guide.Status = false;

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetBookingById), new { id = booking.Id }, booking);
    }

    [HttpGet("bookings/{id}")]
    [Authorize]
    public async Task<IActionResult> GetBookingById(int id)
    {
        var booking = await _context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Guide)
            .Include(b => b.Car)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
        {
            return NotFound();
        }

        // Ensure the booking belongs to the logged-in user
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (booking.CustomerId != userId)
        {
            return Forbid(); 
        }

        return Ok(booking);
    }
    [HttpGet("bookings")]
    [Authorize]
    public async Task<IActionResult> GetAllBookings()
    {
        
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        
        var bookings = await _context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Guide)
            .Include(b => b.Car)
            .Where(b => b.CustomerId == userId)
            .ToListAsync();

        if (bookings == null || !bookings.Any())
        {
            return NotFound(); 
        }

        return Ok(bookings);
    }


    // **Bill Generation**
    [HttpGet("bills/{bookingId}")]
    [Authorize]
    public async Task<IActionResult> GenerateBill(int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Guide)
            .Include(b => b.Car)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            return NotFound();
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (booking.CustomerId != userId)
        {
            return Forbid(); 
        }

        var bill = new
        {
            BookingId = booking.Id,
            GuideName = booking.Guide.Name,
            CarModel = booking.Car.ModelName,
            CustomerName = booking.Customer.Name,
            StartDate = booking.StartDate,
            EndDate = booking.EndDate,
            NumberOfDays = booking.NumberOfDays,
            CarFarePerDay = booking.Car.Fare,
            GuideFarePerDay = booking.Guide.Fare,
            PlatformFee = 200,
            TotalAmount = booking.TotalAmount,
            Date = booking.StartDate
        };

        return Ok(bill);
    }

    [HttpPost("feedback")]
    [Authorize]
    public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackDto feedbackDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        // Check if the booking exists and belongs to the logged-in user
        var booking = await _context.Bookings
            .Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.Id == feedbackDto.BookingId);

        if (booking == null || booking.CustomerId != userId)
        {
            return BadRequest(new { message = "Invalid booking ID or unauthorized access" });
        }

        // Create and save the feedback
        var feedback = new Feedback
        {
            BookingId = feedbackDto.BookingId,
            GuideRating = feedbackDto.GuideRating,
            CarRating = feedbackDto.CarRating,
            Comments = feedbackDto.Comments
        };

        _context.Feedbacks.Add(feedback);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Feedback submitted successfully" });
    }
}

