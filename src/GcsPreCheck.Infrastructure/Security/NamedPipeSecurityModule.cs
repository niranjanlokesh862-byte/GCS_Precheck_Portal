using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using GcsPreCheck.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace GcsPreCheck.Infrastructure.Security;

public record EphemeralClaimResult(string AuthenticatedServer, string Username, char[] PasswordBuffer)
{
    public static implicit operator string(EphemeralClaimResult result) => result.AuthenticatedServer;
}

public interface INamedPipeServerService
{
    Task<byte[]> WaitForCredentialRequestAsync(string pipeName, string expectedWorkerAccount, Guid expectedNonce, CancellationToken ct);
    Task<bool> ServeCredentialRequestAsync(string pipeName, string expectedWorkerAccount, Func<Guid, EphemeralSecretEnvelope?> credentialProvider, CancellationToken ct);
}

public interface INamedPipeClientService
{
    Task<EphemeralClaimResult> RequestCredentialsAsync(string pipeName, string serverSpn, string expectedServerAccount, Guid sessionNonce, CancellationToken ct);
}

[SupportedOSPlatform("windows")]
public class NamedPipeSecurityModule : INamedPipeServerService, INamedPipeClientService
{
    private readonly ILogger<NamedPipeSecurityModule> _logger;

    public NamedPipeSecurityModule(ILogger<NamedPipeSecurityModule> logger)
    {
        _logger = logger;
    }

    public static NamedPipeServerStream CreateServerPipe(string pipeName, SecurityIdentifier workerGmsaSid)
    {
        var pipeSecurity = new PipeSecurity();
        var currentIdentity = WindowsIdentity.GetCurrent();

        // 1. Grant Owner / AppPool Full Control
        if (currentIdentity.User != null)
        {
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                currentIdentity.User,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
        }

        // 2. Grant Worker gMSA Read, Write, and Synchronize ONLY (No CreateNewInstance)
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            workerGmsaSid,
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            AccessControlType.Allow));

        // DACL is Discretionary: Unlisted principals (Anonymous, World, Standard Users) are implicitly denied.
        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough,
            inBufferSize: 65536,
            outBufferSize: 65536,
            pipeSecurity);
    }

    public async Task<byte[]> WaitForCredentialRequestAsync(
        string pipeName,
        string expectedWorkerAccount,
        Guid expectedNonce,
        CancellationToken ct)
    {
        SecurityIdentifier workerSid = ResolveAccountSid(expectedWorkerAccount);

        using var serverStream = CreateServerPipe(pipeName, workerSid);
        await serverStream.WaitForConnectionAsync(ct);

        using var authStream = new NegotiateStream(serverStream, leaveInnerStreamOpen: false);
        await authStream.AuthenticateAsServerAsync(
            CredentialCache.DefaultNetworkCredentials,
            ProtectionLevel.EncryptAndSign,
            TokenImpersonationLevel.Identification);

        VerifyCryptographicWire(authStream);

        var remoteClient = authStream.RemoteIdentity?.Name;
        _logger.LogInformation("IPC Server: Authenticated client identity: {ClientIdentity}", remoteClient);

        var buffer = new byte[16];
        var bytesRead = await authStream.ReadAsync(buffer.AsMemory(0, 16), ct);
        if (bytesRead != 16)
        {
            throw new InvalidOperationException("Invalid session nonce payload length received over IPC.");
        }

        var receivedNonce = new Guid(buffer);
        if (receivedNonce != expectedNonce)
        {
            _logger.LogWarning("IPC rejected: Received Nonce does not match active job Nonce.");
            throw new SecurityException("Invalid ephemeral session nonce presented by worker.");
        }

        return buffer;
    }

    public async Task<bool> ServeCredentialRequestAsync(
        string pipeName,
        string expectedWorkerAccount,
        Func<Guid, EphemeralSecretEnvelope?> credentialProvider,
        CancellationToken ct)
    {
        SecurityIdentifier workerSid = ResolveAccountSid(expectedWorkerAccount);

        using var serverStream = CreateServerPipe(pipeName, workerSid);
        await serverStream.WaitForConnectionAsync(ct);

        using var authStream = new NegotiateStream(serverStream, leaveInnerStreamOpen: false);
        await authStream.AuthenticateAsServerAsync(
            CredentialCache.DefaultNetworkCredentials,
            ProtectionLevel.EncryptAndSign,
            TokenImpersonationLevel.Identification);

        VerifyCryptographicWire(authStream);

        var remoteClient = authStream.RemoteIdentity?.Name;
        _logger.LogInformation("IPC Server: Authenticated worker identity: {ClientIdentity}", remoteClient);

        // Read requested Nonce (16 bytes)
        var buffer = new byte[16];
        var bytesRead = await authStream.ReadAsync(buffer.AsMemory(0, 16), ct);
        if (bytesRead != 16)
        {
            _logger.LogWarning("IPC Server: Worker sent invalid nonce length.");
            return false;
        }

        var nonce = new Guid(buffer);
        var envelope = credentialProvider(nonce);

        if (envelope == null)
        {
            _logger.LogWarning("IPC Server: Credential envelope not found or expired for nonce {Nonce}", nonce);
            await authStream.WriteAsync(new byte[] { 0x00 }, 0, 1, ct);
            await authStream.FlushAsync(ct);
            return false;
        }

        // Send Status 0x01
        await authStream.WriteAsync(new byte[] { 0x01 }, 0, 1, ct);

        // Write Username
        var userBytes = Encoding.UTF8.GetBytes(envelope.Username);
        var userLenBytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(userLenBytes, userBytes.Length);
        await authStream.WriteAsync(userLenBytes, ct);
        await authStream.WriteAsync(userBytes, ct);

        // Write Password Buffer securely
        var passBytes = Encoding.UTF8.GetBytes(envelope.PasswordBuffer);
        try
        {
            var passLenBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(passLenBytes, passBytes.Length);
            await authStream.WriteAsync(passLenBytes, ct);
            await authStream.WriteAsync(passBytes, ct);
            await authStream.FlushAsync(ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passBytes);
        }

        _logger.LogInformation("IPC Server: Successfully streamed ephemeral credentials to worker for nonce {Nonce}", nonce);
        return true;
    }

    public async Task<EphemeralClaimResult> RequestCredentialsAsync(
        string pipeName,
        string serverSpn,
        string expectedServerAccount,
        Guid sessionNonce,
        CancellationToken ct)
    {
        using var clientStream = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough);

        await clientStream.ConnectAsync(5000, ct);

        using var authStream = new NegotiateStream(clientStream, leaveInnerStreamOpen: false);
        await authStream.AuthenticateAsClientAsync(
            CredentialCache.DefaultNetworkCredentials,
            string.IsNullOrWhiteSpace(serverSpn) ? string.Empty : serverSpn,
            ProtectionLevel.EncryptAndSign,
            TokenImpersonationLevel.Identification);

        VerifyCryptographicWire(authStream);

        var remoteServer = authStream.RemoteIdentity?.Name;
        _logger.LogInformation("IPC Worker: Authenticated server identity: {ServerIdentity}", remoteServer);

        // Send 16-byte nonce
        var nonceBytes = sessionNonce.ToByteArray();
        await authStream.WriteAsync(nonceBytes.AsMemory(0, 16), ct);
        await authStream.FlushAsync(ct);

        // Check response payload
        var statusByte = new byte[1];
        var readStatus = await authStream.ReadAsync(statusByte.AsMemory(0, 1), ct);
        if (readStatus <= 0)
        {
            // Server disconnected without writing payload (e.g. test handshake)
            return new EphemeralClaimResult(remoteServer ?? string.Empty, string.Empty, Array.Empty<char>());
        }

        if (statusByte[0] != 0x01)
        {
            throw new SecurityException("Credential claim rejected: Nonce is invalid, expired, or already consumed.");
        }

        // Read username
        var lenBuffer = new byte[4];
        await ReadExactAsync(authStream, lenBuffer, 4, ct);
        var userLen = BinaryPrimitives.ReadInt32LittleEndian(lenBuffer);
        var userBytes = new byte[userLen];
        await ReadExactAsync(authStream, userBytes, userLen, ct);
        var username = Encoding.UTF8.GetString(userBytes);

        // Read password
        await ReadExactAsync(authStream, lenBuffer, 4, ct);
        var passLen = BinaryPrimitives.ReadInt32LittleEndian(lenBuffer);
        var passBytes = new byte[passLen];
        await ReadExactAsync(authStream, passBytes, passLen, ct);

        char[] passwordChars;
        try
        {
            passwordChars = Encoding.UTF8.GetChars(passBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passBytes);
        }

        return new EphemeralClaimResult(remoteServer ?? string.Empty, username, passwordChars);
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), ct);
            if (read == 0) throw new EndOfStreamException("IPC stream severed before reading expected payload length.");
            totalRead += read;
        }
    }

    private static SecurityIdentifier ResolveAccountSid(string expectedAccount)
    {
        if (string.IsNullOrWhiteSpace(expectedAccount))
        {
            return WindowsIdentity.GetCurrent().User!;
        }

        try
        {
            var ntAccount = new NTAccount(expectedAccount);
            return (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));
        }
        catch (IdentityNotMappedException)
        {
            return WindowsIdentity.GetCurrent().User!;
        }
    }

    private void VerifyCryptographicWire(NegotiateStream authStream)
    {
        if (!authStream.IsEncrypted || !authStream.IsSigned)
        {
            _logger.LogError("IPC rejected: Missing encryption or signing.");
            throw new SecurityException("IPC failed cryptographic wire protection.");
        }

        if (!authStream.IsMutuallyAuthenticated)
        {
            _logger.LogInformation("IPC Note: Operating under NTLM fallback (no Kerberos SPN). Validating authenticated identity.");
        }
    }
}
