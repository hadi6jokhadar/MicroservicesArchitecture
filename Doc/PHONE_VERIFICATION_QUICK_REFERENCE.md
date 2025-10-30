# Phone Verification Login - Quick Reference

## 🚀 Quick Start

### Testing the Feature (Development)

1. **Start the Identity Service:**

```bash
cd src/Services/Identity/Identity.API
dotnet run
```

2. **Access Swagger UI:**

```
https://localhost:5101/swagger
```

3. **Apply Database Migration:**

```bash
cd src/Services/Identity/Identity.Infrastructure
$env:MultiTenancy__Enabled="false"
dotnet ef database update --startup-project ../Identity.API
```

## 📋 API Endpoints

### 1️⃣ Get Verification Code

```http
POST /api/auth/get-verification-code
Content-Type: application/json

{
  "phoneNumber": "+1234567890"
}
```

**Response:**

```json
{
  "success": true,
  "message": "Verification code sent successfully"
}
```

### 2️⃣ Login with Code

```http
POST /api/auth/login-with-code
Content-Type: application/json

{
  "phoneNumber": "+1234567890",
  "verificationCode": "12345"
}
```

**Response:**

```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "abc123...",
  "user": { ... }
}
```

### 3️⃣ Register with Code

```http
POST /api/auth/register-with-code
Content-Type: application/json

{
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890"
}
```

**Response:**

```json
{
  "success": true,
  "message": "Registration successful. Please login with the verification code sent to your phone."
}
```

## 🧪 Testing Flow

### New User Registration:

```
1. POST /api/auth/register-with-code
2. Check database for verification code
3. POST /api/auth/login-with-code (with code from DB)
4. Receive JWT tokens
```

### Existing User Login:

```
1. POST /api/auth/get-verification-code
2. Check database for verification code
3. POST /api/auth/login-with-code (with code from DB)
4. Receive JWT tokens
```

## 🔍 Check Verification Code (Development)

**PostgreSQL:**

```sql
SELECT Id, PhoneNumber, VerificationCode, Email, FirstName, LastName
FROM "Users"
WHERE "PhoneNumber" = '+1234567890';
```

**SQL Server:**

```sql
SELECT Id, PhoneNumber, VerificationCode, Email, FirstName, LastName
FROM Users
WHERE PhoneNumber = '+1234567890';
```

## ✅ Validation Rules

| Field             | Rules                                         |
| ----------------- | --------------------------------------------- |
| Phone Number      | Required, E.164 format (`^\+?[1-9]\d{1,14}$`) |
| Verification Code | 5 digits, numeric only (`^\d{5}$`)            |
| Email             | Valid email, max 256 chars                    |
| First/Last Name   | Letters only, max 100 chars                   |

## 🔐 Security Notes

- ✅ Cryptographically secure random code generation
- ✅ Code cleared after successful login
- ✅ Generic error messages (no phone enumeration)
- ✅ Account status validation
- ✅ Same JWT security as password login

## 📂 Key Files

| Layer           | File                                                   | Purpose                |
| --------------- | ------------------------------------------------------ | ---------------------- |
| **Shared**      | `IhsanDev.Shared.Infrastructure/Services/Otp/`         | OTP service            |
| **Domain**      | `Identity.Domain/Entities/User.cs`                     | Added VerificationCode |
| **Application** | `Identity.Application/Commands/Auth/`                  | 3 new commands         |
| **Application** | `Identity.Application/Handlers/Auth/`                  | 3 new handlers         |
| **API**         | `Identity.API/Extensions/EndpointMappingExtensions.cs` | Endpoint mapping       |

## 🛠️ Configuration

**No configuration needed!** Uses existing:

- JWT settings from appsettings.json
- Database connection
- Internal code generation

**Optional (External SMS):**

```csharp
services.AddScoped<IExternalOtpProvider, TwilioOtpProvider>();
```

## 🐛 Troubleshooting

| Error                    | Solution                                    |
| ------------------------ | ------------------------------------------- |
| "Phone number not found" | Use POST /register-with-code first          |
| "Account is disabled"    | Admin must enable account                   |
| "Invalid code"           | Request new code via /get-verification-code |
| Migration fails          | Set `$env:MultiTenancy__Enabled="false"`    |

## 📖 Full Documentation

- **Feature Guide:** `Doc/PHONE_VERIFICATION_LOGIN_GUIDE.md`
- **Implementation Summary:** `Doc/PHONE_VERIFICATION_IMPLEMENTATION_SUMMARY.md`
- **API Docs:** Swagger UI at `/swagger`

## 🎯 Curl Examples

### Register:

```bash
curl -X POST "https://localhost:5101/api/auth/register-with-code" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "firstName": "Test",
    "lastName": "User",
    "phoneNumber": "+1234567890"
  }'
```

### Login:

```bash
curl -X POST "https://localhost:5101/api/auth/login-with-code" \
  -H "Content-Type: application/json" \
  -d '{
    "phoneNumber": "+1234567890",
    "verificationCode": "12345"
  }'
```

## 🚦 Status

- ✅ **Implementation:** Complete
- ✅ **Build:** No Errors
- ✅ **Database:** Migration Created
- ✅ **Documentation:** Complete
- ⏳ **Testing:** Ready for QA
- ⏳ **SMS Integration:** Optional Enhancement

---

**Last Updated:** October 30, 2025  
**Version:** 1.0
