using Xunit;
using Moq;
using XBCAD.Controllers;
using XBCAD.Models;
using XBCAD.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using XBCAD.Controllers;
using XBCAD.ViewModels;

public class ClientControllerTests
{
    [Fact]
    public async Task BookSession_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var mockGoogleCalendarService = new Mock<IGoogleCalendarService>();
        var mockFirebaseService = new Mock<IFirebaseService>();
        var clientController = new ClientController(mockFirebaseService.Object, mockGoogleCalendarService.Object);

        var bookingViewModel = new BookingViewModel
        {
            TrainerId = "trainer123",
            ClientId = "client123",
            StartTime = "2024-11-19T10:00:00",
            EndTime = "2024-11-19T11:00:00",
            PaymentStatus = "Unpaid"
        };

        mockGoogleCalendarService
            .Setup(service => service.CreateCalendarEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("event123");

        mockFirebaseService
            .Setup(service => service.SaveSessionAsync(It.IsAny<BookingViewModel>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await clientController.BookSession(bookingViewModel);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MySessions", redirectResult.ActionName);

        // Verify that the services were called with correct parameters
        mockGoogleCalendarService.Verify(service => service.CreateCalendarEventAsync(
            "trainer123",
            "client123",
            "2024-11-19T10:00:00",
            "2024-11-19T11:00:00"
        ), Times.Once);

        mockFirebaseService.Verify(service => service.SaveSessionAsync(bookingViewModel), Times.Once);
    }
}
