namespace ExampleCoreWebAPI.Helpers
{
    public static class SecurityHelper
    {
        private static IConfiguration config;

        public static void Initialize(IConfiguration Configuration)
        {
            config = Configuration;
        }

        public static string? GetPepper()
        {
            string pepper = config["Security:Pepper"] ?? "";
            if (string.IsNullOrEmpty(pepper))
            {
                //TODO: log pepper not found error somewhere useful
                Console.WriteLine("ERROR: AUTHENTICATION MISCONFIGURATION: \"Security:Pepper\" not found in config");
                return null;
            }
            return pepper;
        }

        public static string SeasonPassword(string password, string salt, string pepper)
        {
            return salt + password + pepper;
        }

        public static string HashPassword(string seasonedPassword)
        {
            return BCrypt.Net.BCrypt.HashPassword(seasonedPassword, 14); //work factor of 14 takes ~1 second to verify 11/30/22
        }
    }
}
