using WireGuard.Shared.Validation;

namespace WireGuard.Service.Tests;

public class WireGuardConfValidatorTests
{
    private const string ValidConf = """
        [Interface]
        PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTGang8=
        Address = 10.0.0.2/32
        DNS = 1.1.1.1

        [Peer]
        PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
        AllowedIPs = 0.0.0.0/0
        Endpoint = vpn.example.com:51820
        PersistentKeepalive = 25
        """;

    [Fact]
    public void ValidConfig_ReturnsValid()
    {
        var result = WireGuardConfValidator.Validate(ValidConf);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void EmptyContent_ReturnsError()
    {
        var result = WireGuardConfValidator.Validate("");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }

    [Fact]
    public void MissingInterface_ReturnsError()
    {
        var conf = """
            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = 0.0.0.0/0
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("[Interface]"));
    }

    [Fact]
    public void MissingPeer_ReturnsError()
    {
        var conf = """
            [Interface]
            PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTGang8=
            Address = 10.0.0.2/32
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("[Peer]"));
    }

    [Fact]
    public void MissingPrivateKey_ReturnsError()
    {
        var conf = """
            [Interface]
            Address = 10.0.0.2/32

            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = 0.0.0.0/0
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("PrivateKey"));
    }

    [Fact]
    public void MissingAddress_ReturnsError()
    {
        var conf = """
            [Interface]
            PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTgang8=

            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = 0.0.0.0/0
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Address"));
    }

    [Fact]
    public void MissingPublicKey_ReturnsError()
    {
        var conf = """
            [Interface]
            PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTgang8=
            Address = 10.0.0.2/32

            [Peer]
            AllowedIPs = 0.0.0.0/0
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("PublicKey"));
    }

    [Fact]
    public void InvalidBase64Key_ReturnsError()
    {
        var conf = """
            [Interface]
            PrivateKey = not-valid-base64!!!
            Address = 10.0.0.2/32

            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = 0.0.0.0/0
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("base64"));
    }

    [Fact]
    public void InvalidCidr_ReturnsError()
    {
        var conf = """
            [Interface]
            PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTGang8=
            Address = not-an-ip/32

            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = 0.0.0.0/0
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not a valid IP"));
    }

    [Fact]
    public void InvalidEndpoint_ReturnsError()
    {
        var conf = """
            [Interface]
            PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTGang8=
            Address = 10.0.0.2/32

            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = 0.0.0.0/0
            Endpoint = invalid-no-port
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidConfigWithIPv6_ReturnsValid()
    {
        var conf = """
            [Interface]
            PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTGang8=
            Address = fd00::2/128

            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = ::/0
            Endpoint = [::1]:51820
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MultiplePeers_AllNeedPublicKey()
    {
        var conf = """
            [Interface]
            PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTGang8=
            Address = 10.0.0.2/32

            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = 10.0.0.0/24

            [Peer]
            AllowedIPs = 10.1.0.0/24
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("peer #2") && e.Contains("PublicKey"));
    }

    [Fact]
    public void UnknownSection_ProducesWarning()
    {
        var conf = """
            [Interface]
            PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTGang8=
            Address = 10.0.0.2/32

            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = 0.0.0.0/0

            [Custom]
            Foo = bar
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void DuplicateInterface_ReturnsError()
    {
        var conf = """
            [Interface]
            PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTGang8=
            Address = 10.0.0.2/32

            [Interface]
            PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTGang8=
            Address = 10.0.0.3/32

            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = 0.0.0.0/0
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate"));
    }

    [Fact]
    public void Comments_AreIgnored()
    {
        var conf = """
            # This is a comment
            [Interface]
            PrivateKey = YNqHbfBQKaGvlC4hHLMQP2mNb2OMemFE3x/bMTGang8=
            Address = 10.0.0.2/32
            # Another comment

            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = 0.0.0.0/0
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void KeyWrongLength_ReturnsError()
    {
        // This is a valid base64 but only 16 bytes
        var conf = """
            [Interface]
            PrivateKey = AAAAAAAAAAAAAAAAAAAAAA==
            Address = 10.0.0.2/32

            [Peer]
            PublicKey = xTIBA5rboUvnH4htodjb6e697QjLERt1NAB4mZqp8Dg=
            AllowedIPs = 0.0.0.0/0
            """;
        var result = WireGuardConfValidator.Validate(conf);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("32 bytes"));
    }
}
