using HNGTASK2.Controllers;
using HNGTASK2.Data;
using HNGTASK2.Dtos;
using HNGTASK2.Models;
using HNGTASK2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Polly;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace HNGTASK2.tests
{
    [Collection("Database Collection")]
    public class auth : IClassFixture<DatabaseFixture>
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly AuthService _authService;
        public auth(DatabaseFixture fixture)
        {
            _context = fixture.Context;

            _mockConfig = new Mock<IConfiguration>();
            _mockConfig.SetupGet(x => x["Jwt:Key"]).Returns("13903375824353900493586022285094325668368165946266468591335796747274938632132");
            _mockConfig.SetupGet(x => x["Jwt:Issuer"]).Returns("testIssuer");
            _mockConfig.SetupGet(x => x["Jwt:Audience"]).Returns("testAudience");
            _mockConfig.SetupGet(x => x["Jwt:ExpireMinutes"]).Returns("30");

            _jwtService = new JwtService(_mockConfig.Object);

            // Mock your DbContext and JwtService as needed
            var mockJwtService = new Mock<JwtService>(_mockConfig.Object);
            var mockDbContext = new Mock<ApplicationDbContext>();
            var authService = new AuthService(mockDbContext.Object, mockJwtService.Object);


        }

        [Fact]
        public void GenerateToken_TokenExpiresAtCorrectTime()
        {
            // Arrange
            var user = new User
            {
                UserId = "someUserId",
                Email = "user@example.com",
                FirstName = "mark",
                LastName = "williams",
                Password = "password1",
                Phone = "09096786890"
            };

            // Act
            var token = _jwtService.GenerateToken(user);

            // Assert
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenData = tokenHandler.ReadJwtToken(token);

            Assert.Equal("testIssuer", tokenData.Issuer);
            Assert.Equal("testAudience", tokenData.Audiences.FirstOrDefault());
            Assert.Equal(user.Email, tokenData.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
            Assert.Equal(user.UserId, tokenData.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value);

            var tokenExpiration = tokenData.ValidTo;
            var expectedExpiration = DateTime.Now.AddMinutes(30); // Ensure this matches your configuration

            Assert.True(tokenExpiration > DateTime.UtcNow.AddMinutes(29)); // Within 30 minutes
            Assert.True(tokenExpiration < DateTime.UtcNow.AddMinutes(31)); // Within 30 minutes
        }


        [Fact]
        public async Task GetOrganisations_UserCanOnlySeeTheirOwnOrganisations()
        {
            // Arrange
            var userId = "user1";
            var token = _jwtService.GenerateToken(new User { UserId = userId, Email = "user1@example.com" });

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, userId),
                new Claim(JwtRegisteredClaimNames.Sub, "user1@example.com"),
            }, "TestAuthType"));

            var controllerContext = new ControllerContext()
            {
                HttpContext = httpContext,
            };

            var controller = new OrganisationController(_context)
            {
                ControllerContext = controllerContext,
            };

            // Act
            var result = await controller.GetOrganisations();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Debug.WriteLine($"Actual status value: '{okResult.Value}'");
            var response = (dynamic)okResult.Value;
            //var response = Assert.IsType<dynamic>(okResult.Value);
            // Debug or log the actual value of response.status
            Debug.WriteLine($"Actual status value: '{response.status}'");

            // Assert with case-sensitive comparison and no leading/trailing whitespace
            Assert.Equal("success", response.status.ToString(), ignoreCase: false);
            Assert.Single(response.data.organisations);
            // Assert.Single((List<object>)response.data.organisations);
            // Assert.Equal(1, response.data.organisations.Count); // user1 should only see 1 organisation
        }

    }
}

