// Services/PasswordHasher.cs (Updated)
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace SRDCS.Services
{
    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string hashedPassword, string providedPassword);
    }

    public class PasswordHasher : IPasswordHasher
    {
        // Method 1: PBKDF2 (New passwords)
        public string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(128 / 8);
            var hash = KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8);

            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        // Method 2: Simple SHA256 (For your existing passwords)
        private string HashWithSHA256(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            try
            {
                // Try PBKDF2 format first (salt:hash)
                if (hashedPassword.Contains(':'))
                {
                    var parts = hashedPassword.Split(':');
                    if (parts.Length == 2)
                    {
                        var salt = Convert.FromBase64String(parts[0]);
                        var hash = Convert.FromBase64String(parts[1]);

                        var newHash = KeyDerivation.Pbkdf2(
                            providedPassword,
                            salt,
                            KeyDerivationPrf.HMACSHA256,
                            10000,
                            hash.Length);

                        return CryptographicOperations.FixedTimeEquals(hash, newHash);
                    }
                }

                // Try SHA256 (for your existing passwords)
                var sha256Hash = HashWithSHA256(providedPassword);
                return hashedPassword == sha256Hash;
            }
            catch
            {
                return false;
            }
        }
    }
}