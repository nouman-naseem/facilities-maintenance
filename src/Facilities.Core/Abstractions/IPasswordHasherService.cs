namespace Facilities.Core.Abstractions;

public interface IPasswordHasherService
{
    string Hash(string password);

    /// <returns>True if <paramref name="providedPassword"/> matches the stored hash.</returns>
    bool Verify(string hash, string providedPassword);
}
