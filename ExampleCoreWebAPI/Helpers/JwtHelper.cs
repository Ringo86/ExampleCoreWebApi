using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;

namespace ExampleCoreWebAPI.Helpers
{
    public static class JwtHelper
    {
        public static SigningCredentials GetSigningCredentials(IConfiguration config)
        {
            var jwtKeyBytes = Encoding.ASCII.GetBytes(config["Jwt:Key"]);
            var secretKey = new SymmetricSecurityKey(jwtKeyBytes);
            //TODO: in the future change to RS256 asymmetric encryption for multi-api security
            var signingCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
            return signingCredentials;
        }

        public static TokenValidationParameters GetTokenValidationParameters(IConfiguration config)
        {
            return new TokenValidationParameters()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = config["Jwt:Issuer"],
                ValidAudience = config["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"])),
                ClockSkew = TimeSpan.Zero,
                //TODO: verify HmacSha256 configured and signature validated properly without allowing any other security algorithms
                //SignatureValidator = (token, p) =>
                //{
                //    var clientSecret = config["Jwt:Key"];
                //    var jwt = new JwtSecurityToken(clientSecret);
                //    var hmac = new HMACSHA256(Convert.FromBase64String(clientSecret));
                //    var signingCredentials = new SigningCredentials(
                //       new SymmetricSecurityKey(hmac.Key), SecurityAlgorithms.HmacSha256Signature, SecurityAlgorithms.Sha256Digest);
                //    var signKey = signingCredentials.Key as SymmetricSecurityKey;
                //    var encodedData = jwt.EncodedHeader + "." + jwt.EncodedPayload;
                //    var compiledSignature = Encode(encodedData, signKey.Key);
                //    //Validate the incoming jwt signature against the header and payload of the token
                //    if (compiledSignature != jwt.RawSignature)
                //    {
                //        throw new Exception("Token signature validation failed.");
                //    }
                //    return jwt;
                //}
            };
        }

        private static string Encode(string input, byte[] key)
        {
            HMACSHA256 myhmacsha = new HMACSHA256(key);
            byte[] byteArray = Encoding.UTF8.GetBytes(input);
            MemoryStream stream = new MemoryStream(byteArray);
            byte[] hashValue = myhmacsha.ComputeHash(stream);
            return Base64UrlEncoder.Encode(hashValue);
        }
    }
}
