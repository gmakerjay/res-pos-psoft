using System;
using System.Text.Json;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using Xunit;

namespace RestaurantPOS.Tests;

public class OrderLifecycleAndAntiDuplicateTests
{
    [Fact]
    public void OrderStatus_TransitionGuards_WhenNew_CanAcceptOnly()
    {
        // Arrange
        var order = new OrderDto
        {
            Id = 1,
            OrderNumber = "ORD-001",
            Status = OrderStatus.New,
            TableNumber = "1"
        };

        // Assert
        Assert.True(order.CanAccept, "New orders must allow accepting");
        Assert.False(order.CanPrepare, "New orders cannot be prepared until accepted");
        Assert.False(order.CanReady, "New orders cannot be marked as ready immediately");
        Assert.False(order.CanComplete, "New orders cannot be marked as completed immediately");
        Assert.True(order.IsActive, "New orders must be active");
    }

    [Fact]
    public void OrderStatus_TransitionGuards_WhenAccepted_CanPrepareOnly_CannotAcceptAgain()
    {
        // Arrange: Order was accepted by either POS or Web
        var order = new OrderDto
        {
            Id = 1,
            OrderNumber = "ORD-001",
            Status = OrderStatus.Accepted,
            TableNumber = "1"
        };

        // Assert: Anti-duplicate guard ensures CanAccept is now false
        Assert.False(order.CanAccept, "Accepted orders must NOT allow duplicate accept action");
        Assert.True(order.CanPrepare, "Accepted orders can transition to Preparing");
        Assert.False(order.CanReady, "Accepted orders cannot jump to Ready before Preparing");
        Assert.False(order.CanComplete, "Accepted orders cannot be completed until ready/served");
        Assert.True(order.IsActive);
    }

    [Fact]
    public void OrderStatus_TransitionGuards_WhenPreparing_CanReady_CannotAcceptOrPrepareAgain()
    {
        // Arrange: Cooking in progress
        var order = new OrderDto
        {
            Id = 2,
            OrderNumber = "ORD-002",
            Status = OrderStatus.Preparing,
            TableNumber = "2"
        };

        // Assert
        Assert.False(order.CanAccept, "Preparing orders must NOT allow accept click");
        Assert.False(order.CanPrepare, "Preparing orders must NOT allow prepare click again");
        Assert.True(order.CanReady, "Preparing orders must allow marking as Ready");
        Assert.False(order.CanComplete);
        Assert.True(order.IsActive);
    }

    [Fact]
    public void OrderStatus_TransitionGuards_WhenReady_CanComplete_CannotAcceptOrPrepare()
    {
        // Arrange: Food is ready for pickup/serving
        var order = new OrderDto
        {
            Id = 3,
            OrderNumber = "ORD-003",
            Status = OrderStatus.Ready,
            TableNumber = "3"
        };

        // Assert
        Assert.False(order.CanAccept);
        Assert.False(order.CanPrepare);
        Assert.False(order.CanReady);
        Assert.True(order.CanComplete, "Ready orders must allow completing/serving");
        Assert.True(order.IsActive);
    }

    [Theory]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public void OrderStatus_TerminalStates_DisallowAllActions(OrderStatus terminalStatus)
    {
        // Arrange
        var order = new OrderDto
        {
            Id = 4,
            OrderNumber = "ORD-004",
            Status = terminalStatus,
            TableNumber = "4"
        };

        // Assert
        Assert.False(order.CanAccept);
        Assert.False(order.CanPrepare);
        Assert.False(order.CanReady);
        Assert.False(order.CanComplete);
        Assert.False(order.IsActive, "Completed/Cancelled orders must be inactive");
    }

    [Fact]
    public void OrderActionActivityDto_SerializationAndMetadataContract_IsConsistent()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var activity = new OrderActionActivityDto
        {
            OrderId = 101,
            OrderNumber = "ORD-101",
            TableDisplay = "โต๊ะ T1",
            PreviousStatus = OrderStatus.New,
            NewStatus = OrderStatus.Accepted,
            Source = "POS หน้าร้าน",
            OperatorName = "สมชาย แคชเชียร์",
            ActionDescription = "รับออเดอร์แล้ว",
            Timestamp = now
        };

        // Act
        var json = JsonSerializer.Serialize(activity);
        var deserialized = JsonSerializer.Deserialize<OrderActionActivityDto>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(activity.OrderId, deserialized.OrderId);
        Assert.Equal(activity.OrderNumber, deserialized.OrderNumber);
        Assert.Equal(activity.TableDisplay, deserialized.TableDisplay);
        Assert.Equal(activity.PreviousStatus, deserialized.PreviousStatus);
        Assert.Equal(activity.NewStatus, deserialized.NewStatus);
        Assert.Equal(activity.Source, deserialized.Source);
        Assert.Equal(activity.OperatorName, deserialized.OperatorName);
        Assert.Equal(activity.ActionDescription, deserialized.ActionDescription);
        Assert.Equal(activity.Timestamp.ToString("s"), deserialized.Timestamp.ToString("s"));
    }
}
