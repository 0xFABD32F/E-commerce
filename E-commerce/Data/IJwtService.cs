using E_commerce.Models;

namespace E_commerce.Data
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}
