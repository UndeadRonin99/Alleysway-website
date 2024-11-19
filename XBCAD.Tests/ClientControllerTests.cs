using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using XBCAD.Controllers;
using XBCAD.ViewModels;
using XBCAD.Models;
using XBCAD.Tests.Mocks;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Moq;
using Microsoft.AspNetCore.Authentication;

namespace XBCAD.Tests.Controllers
{
    public class ClientControllerTests
    {
        private readonly MockFirebaseService _mockFirebaseService;
        private readonly MockGoogleCalendarService _mockGoogleCalendarService;
        private readonly ClientController _clientController;

        public ClientControllerTests()
        {
            // Initialize mock services
            _mockFirebaseService = new MockFirebaseService();
            _mockGoogleCalendarService = new MockGoogleCalendarService();

            // Initialize ClientController with mock services
            _clientController = new ClientController(_mockFirebaseService, _mockGoogleCalendarService);

            // Create user claims
            var userClaims = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, "client1"),
    new Claim(ClaimTypes.Name, "Mock Client"),
    new Claim(ClaimTypes.Email, "client@example.com")
};

            // Create identity and principal
            var identity = new ClaimsIdentity(userClaims, "TestAuthType");
            var principal = new ClaimsPrincipal(identity);

            // Create authentication properties and store the access token
            var authProperties = new AuthenticationProperties();
            authProperties.StoreTokens(new List<AuthenticationToken>
{
    new AuthenticationToken
    {
        Name = "access_token",
        Value = "fake_access_token"
    }
});

            // Create an authentication ticket
            var authTicket = new AuthenticationTicket(principal, authProperties, "TestAuthType");

            // Set up the authentication result
            var authenticateResult = AuthenticateResult.Success(authTicket);

            // Mock the IAuthenticationService
            var authenticationServiceMock = new Mock<IAuthenticationService>();
            authenticationServiceMock
                .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()))
                .ReturnsAsync(authenticateResult);

            // Set up the service provider
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(x => x.GetService(typeof(IAuthenticationService)))
                .Returns(authenticationServiceMock.Object);

            // Create the HttpContext with the mocked services
            var httpContext = new DefaultHttpContext
            {
                User = principal,
                RequestServices = serviceProviderMock.Object
            };

            // Assign the HttpContext to the controller
            _clientController.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };
        }

        

        [Fact]
        public async Task GetFutureSessions_ReturnsFutureSessions()
        {
            // Arrange
            var currentDateTime = DateTime.Now;
            await _mockFirebaseService.PutBookedSession(new BookedSession
            {
                TrainerID = "trainer1",
                ClientID = "client1",
                Paid = true,
                TotalAmount = 150.0m,
                StartDateTime = currentDateTime.AddDays(1).ToString("o"),
                EndDateTime = currentDateTime.AddDays(1).AddHours(1).ToString("o")
            }, "trainer1", "client1", "Client One", currentDateTime);

            // Act
            var futureSessions = await _mockFirebaseService.GetFutureBookedSessionsForTrainerAsync("trainer1", currentDateTime);

            // Assert
            Assert.NotEmpty(futureSessions);
            Assert.Single(futureSessions);
            Assert.Equal("client1", futureSessions.First().ClientID);
        }

        [Fact]
        public async Task GetCalendarEmbedLink_ReturnsCorrectLink()
        {
            // Arrange
            string accessToken = "mockAccessToken";
            string email = "trainer@example.com";

            // Act
            var embedLink = await _mockGoogleCalendarService.GetCalendarEmbedLinkAsync(accessToken, email);

            // Assert
            Assert.Equal($"https://calendar.google.com/calendar/embed?src=trainer%40example.com&ctz=Africa/Johannesburg", embedLink);
        }

        [Fact]
        public async Task GetClientsForTrainer_ReturnsClientList()
        {
            // Arrange
            // Add client to mock service
            _mockFirebaseService.AddClient(new ClientViewModel
            {
                Id = "client1",
                Name = "Client One",
                ProfileImageUrl = "/images/default.jpg"
            });

            // Add a session between trainer1 and client1
            var currentDateTime = DateTime.Now;
            await _mockFirebaseService.PutBookedSession(new BookedSession
            {
                TrainerID = "trainer1",
                ClientID = "client1",
                Paid = true,
                TotalAmount = 150.0m,
                StartDateTime = currentDateTime.AddDays(1).ToString("o"),
                EndDateTime = currentDateTime.AddDays(1).AddHours(1).ToString("o")
            }, "trainer1", "client1", "Client One", currentDateTime);

            // Act
            var clients = await _mockFirebaseService.GetClientsForTrainerAsync("trainer1");

            // Assert
            Assert.NotEmpty(clients);
            Assert.Single(clients);
            Assert.Equal("client1", clients.First().Id);
        }
    }
}
