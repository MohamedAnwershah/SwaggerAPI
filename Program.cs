using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // <-- Required for Database operations
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.Security.Permissions;
using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.OpenApi.Models;

var jwtKey = "ThisIsMySuperSecretKey123456789012";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<RecipeService>();
builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true
    };
});
    
    builder.Services.AddAuthorization();
    builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/login", async (LoginDto request, AppDbContext db) =>
{
    Console.WriteLine($"[Login Attempt] Username: {request.Username}, Password: {request.Password}");

    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

    if (user == null)
    {
        Console.WriteLine("[Error] User not found in database!");
        return Results.Unauthorized();
    }
    Console.WriteLine($"[Success] User found: {user.Username}. Stored Hash: {user.PasswordHash}");

    bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

    if (!isPasswordValid)
    {
        Console.WriteLine("[Error] Password incorrect! Hash verification failed.");
        return Results.Unauthorized();
    }

    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(jwtKey); 
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, "User")
        }),
        Expires = DateTime.UtcNow.AddHours(1),
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature
        )
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);
    var tokenString = tokenHandler.WriteToken(token);

    Console.WriteLine("[Success] Token generated!");
    return Results.Ok(new { Token = tokenString });
});


app.MapPost("/register", async (LoginDto request, AppDbContext db) =>
{
    if (await db.Users.AnyAsync(u => u.Username == request.Username))
    {
        return Results.BadRequest("Username already exists!");
    }

    string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

    var newUser = new User
    {
        Username = request.Username,
        PasswordHash = passwordHash
    };

    db.Users.Add(newUser);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "User registered successfully!" });
});

app.MapGet("/api/recipes", async (RecipeService service) =>
{
    var recipes = await service.GetAllRecipesAsync();
    return Results.Ok(recipes);
}
);

app.MapGet("/api/recipes/search", async (RecipeService service, [FromQuery] int maxCalorie) =>
{
    var filteredList = await service.SearchRecipesAsync(maxCalorie);
    return Results.Ok(filteredList);
}
);

app.MapPost("/api/recipes", async (CreateRecipeDTO request, RecipeService service) =>
{
    await service.AddRecipeAsync(request);
    return Results.Ok(new {message = "Successfully added!"});
}
).RequireAuthorization();

app.Run();

class RecipeSubmission
{
    public int Id {get; set;}
    required public string RecipeName { get; set; }
    public int ExpectedCalories { get; set; }
}

class User
{
    public int Id {get; set;}
    public required string Username {get; set;}
    public required string PasswordHash {get; set;}
}

class CreateRecipeDTO
{
    public required string RecipeName { get; set; }
    public int ExpectedCalories { get; set; }
}

class RecipeService(AppDbContext db)
{
    private readonly AppDbContext _db = db;
    public async Task<List<RecipeSubmission>> GetAllRecipesAsync()
    {
        return await _db.Recipes.ToListAsync();
    }

    public async Task AddRecipeAsync(CreateRecipeDTO request)
    {
        var newRecipe = new RecipeSubmission
        {
            RecipeName = request.RecipeName,
            ExpectedCalories = request.ExpectedCalories
        };
        _db.Recipes.Add(newRecipe);
        await _db.SaveChangesAsync();
    }
    public async Task<List<RecipeSubmission>> SearchRecipesAsync(int maxCalorie)
    { 
        return await _db.Recipes.Where(r => r.ExpectedCalories <= maxCalorie).ToListAsync();
    }
}

class AppDbContext : DbContext
{
    public DbSet<RecipeSubmission> Recipes { get; set; }
    public DbSet<User> Users { get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=recipes.db");
    }
}
class LoginDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}
