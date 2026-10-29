using System.Net;
using Inspecta.Infrastructure.Mail;

namespace Inspecta.Domain.Tests;

public sealed class PublicAddressGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.5")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:10.0.0.1")]
    public void InternalAddressesAreRejected(string address) =>
        Assert.True(PublicAddressGuard.IsNonPublic(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("142.250.74.109")]
    [InlineData("2a00:1450:4001:82b::200e")]
    public void PublicAddressesAreAllowed(string address) =>
        Assert.False(PublicAddressGuard.IsNonPublic(IPAddress.Parse(address)));
}
