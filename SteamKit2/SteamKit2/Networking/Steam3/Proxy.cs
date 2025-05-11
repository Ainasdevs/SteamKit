namespace SteamKit2;

/// <summary>
/// Specifies the proxy scheme used when connecting to Steam.
/// </summary>
public enum ProxyScheme
{
    /// <summary>
    /// HTTP proxy.
    /// </summary>
    Http,

    /// <summary>
    /// SOCKS4 proxy.
    /// </summary>
    Socks4,

    /// <summary>
    /// SOCKS5 proxy.
    /// </summary>
    Socks5
}

/// <summary>
/// Represents a proxy server to use when connecting to Steam.
/// </summary>
public record Proxy
{
    /// <summary>
    /// The address of the proxy server.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// The port of the proxy server.
    /// </summary>
    public required ushort Port { get; init; }

    /// <summary>
    /// The scheme of the proxy server.
    /// </summary>
    public required ProxyScheme Scheme { get; init; }

    /// <summary>
    /// The username to use when connecting to the proxy server.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// The password to use when connecting to the proxy server.
    /// </summary>
    public string? Password { get; init; }
}
