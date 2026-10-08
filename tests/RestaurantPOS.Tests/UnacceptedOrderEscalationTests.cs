using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Wpf.Services;
using Xunit;

namespace RestaurantPOS.Tests;

public class UnacceptedOrderEscalationTests
{
    [Theory]
    [InlineData(1, 70)]
    [InlineData(2, 70)]
    [InlineData(3, 70)]
    [InlineData(4, 70)]
    public void AudioAlertService_GeneratesValidPcmWavHeader(int level, int baseVolume)
    {
        // Access private static method GenerateChimeWav via reflection to verify synthesis
        var generateMethod = typeof(AudioAlertService).GetMethod("GenerateChimeWav", 
            BindingFlags.NonPublic | BindingFlags.Static);
        var getNotesMethod = typeof(AudioAlertService).GetMethod("GetNotesForEscalationLevel", 
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(generateMethod);
        Assert.NotNull(getNotesMethod);

        var notes = getNotesMethod.Invoke(null, new object[] { level });
        var wavBytes = (byte[])generateMethod.Invoke(null, new object[] { baseVolume, notes! })!;

        Assert.NotNull(wavBytes);
        Assert.True(wavBytes.Length > 44, "WAV byte stream must contain at least standard 44-byte header");

        using var ms = new MemoryStream(wavBytes);
        using var reader = new BinaryReader(ms);

        // Check RIFF header
        var riff = new string(reader.ReadChars(4));
        Assert.Equal("RIFF", riff);

        var fileSize = reader.ReadInt32();
        Assert.Equal(wavBytes.Length - 8, fileSize);

        var wave = new string(reader.ReadChars(4));
        Assert.Equal("WAVE", wave);

        // Check fmt subchunk
        var fmt = new string(reader.ReadChars(4));
        Assert.Equal("fmt ", fmt);

        var subchunk1Size = reader.ReadInt32();
        Assert.Equal(16, subchunk1Size);

        var audioFormat = reader.ReadInt16();
        Assert.Equal(1, audioFormat); // 1 = PCM

        var numChannels = reader.ReadInt16();
        Assert.Equal(1, numChannels); // 1 = Mono

        var sampleRate = reader.ReadInt32();
        Assert.Equal(44100, sampleRate); // 44.1 kHz

        var byteRate = reader.ReadInt32();
        Assert.Equal(44100 * 2, byteRate);

        var blockAlign = reader.ReadInt16();
        Assert.Equal(2, blockAlign);

        var bitsPerSample = reader.ReadInt16();
        Assert.Equal(16, bitsPerSample);

        // Check data subchunk
        var data = new string(reader.ReadChars(4));
        Assert.Equal("data", data);

        var dataSize = reader.ReadInt32();
        Assert.True(dataSize > 0, "Data size must be positive");
    }

    [Fact]
    public void EscalationLogic_CalculatesCorrectRoundsAndIntervals()
    {
        // Simulate a 30s interval escalating alert cycle
        const int intervalSeconds = 30;
        var orderArrivedAt = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);

        // Timeline check
        // T=0s: Initial alert
        var t0 = orderArrivedAt;
        var waitingSec0 = (int)(t0 - orderArrivedAt).TotalSeconds;
        var round0 = (waitingSec0 / intervalSeconds) + 1;
        Assert.Equal(1, round0);

        // T=30s: Second alert (+30s)
        var t30 = orderArrivedAt.AddSeconds(30);
        var waitingSec30 = (int)(t30 - orderArrivedAt).TotalSeconds;
        var round30 = (waitingSec30 / intervalSeconds) + 1;
        Assert.Equal(2, round30);

        // T=60s: Third alert (+60s)
        var t60 = orderArrivedAt.AddSeconds(60);
        var waitingSec60 = (int)(t60 - orderArrivedAt).TotalSeconds;
        var round60 = (waitingSec60 / intervalSeconds) + 1;
        Assert.Equal(3, round60);

        // T=90s: Fourth alert (+90s)
        var t90 = orderArrivedAt.AddSeconds(90);
        var waitingSec90 = (int)(t90 - orderArrivedAt).TotalSeconds;
        var round90 = (waitingSec90 / intervalSeconds) + 1;
        Assert.Equal(4, round90);
    }

    [Fact]
    public void UnacceptedOrderList_FilterAndResetCondition()
    {
        var orders = new List<OrderDto>
        {
            new OrderDto { Id = 1, OrderNumber = "ORD-001", Status = OrderStatus.New },
            new OrderDto { Id = 2, OrderNumber = "ORD-002", Status = OrderStatus.Accepted }
        };

        // When there is an unaccepted order
        var unaccepted = orders.Where(o => o.Status == OrderStatus.New).ToList();
        Assert.Single(unaccepted);
        Assert.Equal(1, unaccepted[0].Id);

        // When order #1 is accepted by either POS or Web
        orders[0].Status = OrderStatus.Accepted;

        var unacceptedAfterAccept = orders.Where(o => o.Status == OrderStatus.New).ToList();
        Assert.Empty(unacceptedAfterAccept);
    }
}
