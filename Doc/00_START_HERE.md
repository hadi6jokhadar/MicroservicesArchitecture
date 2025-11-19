# 📚 Microservices Architecture - Documentation Index

**Last Updated:** November 2025  
**Version:** 2.2

---

## 🎯 Quick Navigation

### **New to the Project? Start Here:**

1. 📖 Read this file (you are here!)
2. 🏗️ [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) - **CRITICAL:** Understand your multi-database architecture
3. 🔐 [SHARED_IDENTITY_SERVICE_GUIDE.md](SHARED_IDENTITY_SERVICE_GUIDE.md) - Authentication & Authorization
4. 🚀 [NEW_SERVICE_INTEGRATION_GUIDE.md](NEW_SERVICE_INTEGRATION_GUIDE.md) - Creating new microservices
5. ⚠️ [BYPASS_TENANT_ENDPOINTS_GUIDE.md](BYPASS_TENANT_ENDPOINTS_GUIDE.md) - **CRITICAL:** Admin/global endpoints patterns
6. 🔔 [BOTTLENECKS_COMPLETION_SUMMARY.md](BOTTLENECKS_COMPLETION_SUMMARY.md) - **NEW:** Performance optimization achievements

### **Need Something Specific?**

- 🏢 **Multi-Tenancy?** → [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md)
- 🆕 **Create New Service?** → [NEW_SERVICE_DESIGN_PATTERN_STAGE_1.md](NEW_SERVICE_DESIGN_PATTERN_STAGE_1.md)
- ⚠️ **Admin/Global Endpoints?** → [BYPASS_TENANT_ENDPOINTS_GUIDE.md](BYPASS_TENANT_ENDPOINTS_GUIDE.md) or [BYPASS_TENANT_QUICK_REFERENCE.md](BYPASS_TENANT_QUICK_REFERENCE.md)
- 📁 **File Storage?** → [FILE_MANAGER_QUICK_REFERENCE.md](FILE_MANAGER_QUICK_REFERENCE.md) or [FILE_MANAGER_SERVICE_GUIDE.md](FILE_MANAGER_SERVICE_GUIDE.md)
- 🔑 **Project Isolation?** → [PROJECT_ISOLATION_STRATEGY_GUIDE.md](PROJECT_ISOLATION_STRATEGY_GUIDE.md)
- 🌍 **Localization?** → [COMPLETE_LOCALIZATION_MIGRATION_SUMMARY.md](COMPLETE_LOCALIZATION_MIGRATION_SUMMARY.md) or [LOCALIZATION_QUICK_REFERENCE.md](LOCALIZATION_QUICK_REFERENCE.md)
- 🔔 **Notifications?** → [NOTIFICATION_SERVICE_README.md](NOTIFICATION_SERVICE_README.md)
- 🔥 **Firebase Push?** → [FIREBASE_QUICK_REFERENCE.md](FIREBASE_QUICK_REFERENCE.md)
- 📱 **Device Tokens?** → [DEVICE_TOKEN_QUICK_REFERENCE.md](DEVICE_TOKEN_QUICK_REFERENCE.md)
- 🧪 **Testing?** → [SHARED_TESTING_FILES.md](SHARED_TESTING_FILES.md)
- ⚡ **Performance?** → [BOTTLENECKS_COMPLETION_SUMMARY.md](BOTTLENECKS_COMPLETION_SUMMARY.md) or [PARALLEL_PROCESSING_OPTIMIZATION_SUMMARY.md](PARALLEL_PROCESSING_OPTIMIZATION_SUMMARY.md)
- 🛡️ **Rate Limiting?** → [RATE_LIMITING_IMPLEMENTATION_SUMMARY.md](RATE_LIMITING_IMPLEMENTATION_SUMMARY.md)
- 🚀 **Redis Caching?** → [REDIS_CACHE_QUICK_REFERENCE.md](REDIS_CACHE_QUICK_REFERENCE.md)
- 🔄 **Redis vs Memory Cache?** → [REDIS_ENABLED_VS_DISABLED_GUIDE.md](REDIS_ENABLED_VS_DISABLED_GUIDE.md)
- 💾 **Database Replication?** → [DATABASE_REPLICATION_SETUP_GUIDE.md](DATABASE_REPLICATION_SETUP_GUIDE.md)

---

## 📁 Documentation Structure

```
Doc/
├─ 00_START_HERE.md                         ← You are here
│
├─ Core Architecture (MUST READ)
│  ├─ DATABASE_PER_TENANT_ARCHITECTURE.md   ← 🔴 CRITICAL: Your architecture explained
│  ├─ AUTOMATIC_DATABASE_MIGRATION.md       ← 🔴 NEW: Auto database creation for tenants
│  ├─ SHARED_IDENTITY_SERVICE_GUIDE.md      ← Authentication for all services
│  ├─ NEW_SERVICE_INTEGRATION_GUIDE.md      ← Step-by-step new service creation
│  ├─ BYPASS_TENANT_ENDPOINTS_GUIDE.md      ← 🔴 NEW: Admin/global endpoints (Nov 2025)
│  │
│  ├─ 🆕 New Service Design Patterns (Complete 3-Stage Guide)
│  │  ├─ NEW_SERVICE_DESIGN_PATTERN_STAGE_1.md  ← 🔴 NEW: Stage 1 - Architecture & Structure
│  │  ├─ NEW_SERVICE_DESIGN_PATTERN_STAGE_2.md  ← 🔴 NEW: Stage 2 - Configuration & Integration
│  │  └─ NEW_SERVICE_DESIGN_PATTERN_STAGE_3.md  ← 🔴 NEW: Stage 3 - Implementation & Testing
│
├─ Service-Specific Guides
│  ├─ MULTI_TENANCY_GUIDE.md                ← Comprehensive multi-tenancy
│  ├─ MULTI_TENANCY_STRICT_MODE.md          ← 🔴 NEW: Strict mode behavior & migration
│  ├─ MULTI_TENANCY_QUICK_START.md          ← Quick setup
│  ├─ MULTI_TENANT_DEPLOYMENT_GUIDE.md      ← Deployment strategies
│  ├─ JWT_TENANT_VERIFICATION_IMPLEMENTATION.md  ← 🔴 NEW: Prevent tenant impersonation
│  ├─ JWT_TENANT_VERIFICATION_GUIDE.md      ← JWT tenant-specific settings verification
│  ├─ JWT_AND_NOTIFICATION_FLOW_EXAMPLE.md  ← JWT flow example walkthrough
│  ├─ JWT_SECRET_AND_VALIDATION_FLOW.md     ← JWT validation explained
│  ├─ FILE_MANAGER_SERVICE_GUIDE.md         ← File storage architecture
│  ├─ FILE_MANAGER_QUICK_REFERENCE.md       ← 🔴 NEW: File Manager API quick reference
│  ├─ PROJECT_ISOLATION_STRATEGY_GUIDE.md   ← User isolation patterns
│  ├─ TENANT_MIDDLEWARE_EXPLAINED.md        ← How tenant middleware works
│  ├─ TENANT_AWARE_CORS_GUIDE.md            ← Tenant-specific CORS
│  ├─ PHONE_VERIFICATION_LOGIN_GUIDE.md     ← Phone/Email OTP authentication
│  ├─ OTP_SECURITY_AND_VALIDATION_UPDATE.md ← 🔴 NEW: OTP security system
│  ├─ PHONE_VERIFICATION_QUICK_REFERENCE.md ← Quick OTP reference
│  ├─ DEVICE_TOKEN_REFACTORING_SUMMARY.md   ← 🔴 NEW: Device token complete summary
│  ├─ DEVICE_TOKEN_MANAGEMENT_GUIDE.md      ← 🔴 NEW: Device token developer guide
│  ├─ DEVICE_TOKEN_QUICK_REFERENCE.md       ← 🔴 NEW: Device token API reference
│  ├─ NOTIFICATION_SERVICE_README.md        ← Complete notification service guide
  ├─ FIREBASE_QUICK_REFERENCE.md           ← 🔴 NEW: Firebase FCM quick start
  ├─ FIREBASE_PUSH_NOTIFICATIONS_GUIDE.md  ← Complete Firebase FCM integration guide
  ├─ FIREBASE_PUSH_NOTIFICATION_FLOW.md    ← End-to-end Firebase flow diagram
  ├─ FIREBASE_NOTIFICATION_SCENARIOS.md    ← 🔴 NEW: Global/Tenant/User notification flows
  ├─ NOTIFICATION_SYSTEM_FLOW.md           ← Complete notification system
│  ├─ NOTIFICATION_HUB_GUIDE.md             ← SignalR hub comprehensive guide
│  ├─ NOTIFICATION_HUB_QUICK_REFERENCE.md   ← Quick notification reference
│  ├─ DEVICE_TOKEN_MANAGEMENT_GUIDE.md      ← 🔴 NEW: Device token management
│  ├─ DEVICE_TOKEN_QUICK_REFERENCE.md       ← 🔴 NEW: Quick device token API reference
│  ├─ JWT_AND_NOTIFICATION_FLOW_EXAMPLE.md  ← JWT flow example walkthrough
│  └─ JWT_SECRET_AND_VALIDATION_FLOW.md     ← JWT validation explained
│
├─ Development Guides
│  ├─ AUTOMAPPER_REMOVAL_SUMMARY.md         ← ✅ AutoMapper removal complete
│  ├─ DATETIME_STANDARDIZATION_SUMMARY.md   ← ✅ DateTime ISO 8601 UTC format
│  ├─ COMPLETE_LOCALIZATION_MIGRATION_SUMMARY.md  ← ✅ NEW: Complete i18n migration (95 keys, en+ar)
│  ├─ LOCALIZATION_VALIDATION_MIGRATION_COMPLETE.md  ← ✅ Validator localization (47 validators)
│  ├─ LOCALIZATION_GUIDE.md                 ← Multi-language support guide
│  ├─ LOCALIZATION_QUICK_REFERENCE.md       ← Localization quick reference
│  ├─ REDIS_CACHE_QUICK_REFERENCE.md        ← 🔴 NEW: Developer quick reference
│  ├─ REDIS_ENABLED_VS_DISABLED_GUIDE.md    ← 🔴 NEW: Redis vs MemoryCache behavior
│  ├─ DATABASE_REPLICATION_SETUP_GUIDE.md   ← 🔴 NEW: PostgreSQL replication guide
│  ├─ BOTTLENECKS_COMPLETION_SUMMARY.md     ← 🔴 NEW: All 11 optimizations completed
│  ├─ PARALLEL_PROCESSING_OPTIMIZATION_SUMMARY.md ← 🔴 NEW: Multi-tenant parallel processing
│  ├─ PERFORMANCE_OPTIMIZATION_GUIDE.md     ← 🔴 NEW: Performance tuning guide
│  ├─ RATE_LIMITING_IMPLEMENTATION_SUMMARY.md ← 🔴 NEW: Rate limiting across all services
│  ├─ CUSTOM_LOGGER_USAGE.md                ← Logging best practices
│  └─ MINIMAL_API_MIGRATION.md              ← Migrating to Minimal APIs
│
├─ Testing Documentation
│  ├─ SHARED_TESTING_ANALYSIS.md            ← Testing infrastructure
│  ├─ SHARED_TESTING_FILES.md               ← Test helpers & patterns
│  ├─ SHARED_TESTING_MIGRATION.md           ← Test migration guide
│  └─ INTEGRATION_TESTING_PROMPT.md         ← Integration testing guide
│
└─ README.md                                 ← Project overview
```

---

## 🏗️ Your Architecture at a Glance

### **The Three Pillars**

```
┌─────────────────────────────────────────────────────────────────┐
│                   SHARED SERVICES (ONE Each)                    │
│                                                                   │
│  ┌────────────────────┐  ┌──────────────────────┐  ┌────────┐  │
│  │ Identity Service   │  │  Tenant Service      │  │  File  │  │
│  │ (Port 5001)        │  │  (Port 5002)         │  │Manager│  │
│  │ • JWT Auth         │  │  • Tenant Config     │  │(5005) │  │
│  │ • User Management  │  │  • Multi-tenancy     │  │        │  │
│  │ • Multi-tenant ✓   │  │  • Single DB (own)   │  │        │  │
│  └────────────────────┘  └──────────────────────┘  └────────┘  │
└─────────────────────────────────────────────────────────────────┘
                  │                        │
    ┌─────────────┴────────────────────────┴─────────────────┐
    │                                                          │
    ▼                                                          ▼
┌──────────────┐                                      ┌──────────────┐
│ Database 1   │  Tenant 123 (Acme Corp)              │ Database 2   │
│              │                                       │              │
│ tenant_123   │  ├─ Users                            │ tenant_456   │
│              │  ├─ Orders (ProjectA)                │              │
│              │  ├─ Products (ProjectB)              │ tenant_456   │
│              │  └─ Files (ProjectC)                 │ (Widget Inc) │
└──────────────┘                                      └──────────────┘
```

### **Key Concepts**

| Concept                 | Description                                              |
| ----------------------- | -------------------------------------------------------- |
| **Database-Per-Tenant** | Each tenant has separate database (complete isolation)   |
| **Shared Services**     | Identity, Tenant, File Manager used by ALL projects      |
| **TenantId**            | Database boundary (different databases)                  |
| **ProjectId**           | Logical filter within same database (soft isolation)     |
| **Dynamic Connection**  | Services connect to different DBs based on tenant        |
| **Tenant Service**      | Provider of configs, NOT a consumer (uses own static DB) |

---

## 🎓 Learning Path by Role

### **For Backend Developers**

**Week 1: Foundation**

1. ✅ [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) - Understand multi-database pattern
2. ✅ [AUTOMATIC_DATABASE_MIGRATION.md](AUTOMATIC_DATABASE_MIGRATION.md) - How tenant databases are auto-created
3. ✅ [SHARED_IDENTITY_SERVICE_GUIDE.md](SHARED_IDENTITY_SERVICE_GUIDE.md) - JWT authentication
4. ✅ [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - Multi-tenancy concepts

**Week 2: Development** 4. ✅ [NEW_SERVICE_INTEGRATION_GUIDE.md](NEW_SERVICE_INTEGRATION_GUIDE.md) - Create your first service 5. ✅ [TENANT_MIDDLEWARE_EXPLAINED.md](TENANT_MIDDLEWARE_EXPLAINED.md) - How tenant resolution works 6. ✅ [PROJECT_ISOLATION_STRATEGY_GUIDE.md](PROJECT_ISOLATION_STRATEGY_GUIDE.md) - User isolation patterns

**Week 3: Advanced** 7. ✅ [CACHING_STRATEGY_COMPARISON.md](CACHING_STRATEGY_COMPARISON.md) - Performance optimization 8. ✅ [FILE_MANAGER_SERVICE_GUIDE.md](FILE_MANAGER_SERVICE_GUIDE.md) - File storage patterns 9. ✅ [SHARED_TESTING_FILES.md](SHARED_TESTING_FILES.md) - Testing best practices

### **For DevOps Engineers**

**Priority Documents:**

1. ✅ [MULTI_TENANT_DEPLOYMENT_GUIDE.md](MULTI_TENANT_DEPLOYMENT_GUIDE.md) - Deployment strategies
2. ✅ [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) - Database architecture
3. ✅ [CACHING_STRATEGY_COMPARISON.md](CACHING_STRATEGY_COMPARISON.md) - Caching infrastructure
4. ✅ [MULTI_TENANCY_QUICK_START.md](MULTI_TENANCY_QUICK_START.md) - Quick deployment

### **For Architects**

**Strategic Documents:**

1. ✅ [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) - Core architecture
2. ✅ [AUTOMATIC_DATABASE_MIGRATION.md](AUTOMATIC_DATABASE_MIGRATION.md) - Automated tenant provisioning
3. ✅ [SHARED_IDENTITY_SERVICE_GUIDE.md](SHARED_IDENTITY_SERVICE_GUIDE.md) - Authentication architecture
4. ✅ [PROJECT_ISOLATION_STRATEGY_GUIDE.md](PROJECT_ISOLATION_STRATEGY_GUIDE.md) - Isolation patterns
5. ✅ [FILE_MANAGER_SERVICE_GUIDE.md](FILE_MANAGER_SERVICE_GUIDE.md) - Storage architecture
6. ✅ [CACHING_STRATEGY_COMPARISON.md](CACHING_STRATEGY_COMPARISON.md) - Performance architecture

---

## 🔍 Common Scenarios & Solutions

### **Scenario 1: "I need to create a new microservice"**

**Solution Path (NEW - 3-Stage Design Pattern):**

1. 🏗️ [NEW_SERVICE_DESIGN_PATTERN_STAGE_1.md](NEW_SERVICE_DESIGN_PATTERN_STAGE_1.md) - Architecture & Structure
2. 🔧 [NEW_SERVICE_DESIGN_PATTERN_STAGE_2.md](NEW_SERVICE_DESIGN_PATTERN_STAGE_2.md) - Configuration & Integration
3. 🚀 [NEW_SERVICE_DESIGN_PATTERN_STAGE_3.md](NEW_SERVICE_DESIGN_PATTERN_STAGE_3.md) - Implementation & Testing

**Alternative (Quick Reference):**

- [NEW_SERVICE_INTEGRATION_GUIDE.md](NEW_SERVICE_INTEGRATION_GUIDE.md) - Step-by-step guide

**Key Files:**

- Authentication: `SHARED_IDENTITY_SERVICE_GUIDE.md`
- Multi-tenancy: `TENANT_MIDDLEWARE_EXPLAINED.md`
- Testing: `SHARED_TESTING_FILES.md`

### **Scenario 2: "Users have same email in different projects"**

**Solution Path:**

1. Read [PROJECT_ISOLATION_STRATEGY_GUIDE.md](PROJECT_ISOLATION_STRATEGY_GUIDE.md)
2. Understand: **Same email in different TENANTS** = different users (different databases)
3. Understand: **Same email in different PROJECTS** = same user (same database)

**Key Insight:**

- **TenantId** = Database boundary (complete isolation)
- **ProjectId** = Filter column (soft isolation within same tenant DB)

### **Scenario 3: "How does tenant routing work?"**

**Solution Path:**

1. Read [DATABASE_PER_TENANT_ARCHITECTURE.md](DATABASE_PER_TENANT_ARCHITECTURE.md) - Section "How It Works"
2. Read [TENANT_MIDDLEWARE_EXPLAINED.md](TENANT_MIDDLEWARE_EXPLAINED.md) - Middleware flow
3. Check `TenantConfigurationProvider.cs` implementation

**Request Flow:**

```
Request → Middleware extracts TenantId → Fetches DB connection
→ Creates DbContext with tenant DB → Queries tenant's database
```

### **Scenario 4: "Should I use MemoryCache or Redis?"**

**Solution Path:**

1. Read [REDIS_ENABLED_VS_DISABLED_GUIDE.md](REDIS_ENABLED_VS_DISABLED_GUIDE.md) - **START HERE** for detailed comparison
2. Read [CACHING_STRATEGY_COMPARISON.md](CACHING_STRATEGY_COMPARISON.md) - Original cache analysis
3. **Quick Answer:**
   - **Development/Single Instance:** Set `"Redis:Enabled": false` (uses MemoryCache automatically)
   - **Production/Multiple Instances:** Set `"Redis:Enabled": true` (uses Redis distributed cache)
4. **No code changes needed!** Just toggle the configuration flag

### **Scenario 5: "How do I handle file uploads?"**

**Solution Path:**

1. Read [FILE_MANAGER_SERVICE_GUIDE.md](FILE_MANAGER_SERVICE_GUIDE.md)
2. Use ONE shared File Manager Service
3. Store files in: `tenant_123/ProjectA/file.pdf`

### **Scenario 6: "How do I test my service?"**

**Solution Path:**

1. Read [NEW_SERVICE_INTEGRATION_GUIDE.md](NEW_SERVICE_INTEGRATION_GUIDE.md) - Part 3
2. Read [SHARED_TESTING_FILES.md](SHARED_TESTING_FILES.md)
3. Use `TenantTestHelper` for generating test data

### **Scenario 7: "How do I implement real-time notifications?"**

**Solution Path:**

1. Read [NOTIFICATION_SYSTEM_FLOW.md](NOTIFICATION_SYSTEM_FLOW.md) - Complete system architecture
2. Read [NOTIFICATION_HUB_GUIDE.md](NOTIFICATION_HUB_GUIDE.md) - SignalR hub implementation
3. Read [NOTIFICATION_HUB_QUICK_REFERENCE.md](NOTIFICATION_HUB_QUICK_REFERENCE.md) - Quick examples
4. Check [JWT_AND_NOTIFICATION_FLOW_EXAMPLE.md](JWT_AND_NOTIFICATION_FLOW_EXAMPLE.md) for detailed walkthrough

**Key Features:**

- Queue-based processing with background services
- SignalR for real-time push notifications
- Optional Firebase Cloud Messaging integration
- Multi-tenancy support with tenant-specific targeting
- Anonymous and authenticated connections
- Five notification targeting scenarios

---

## 📊 Document Maturity & Status

| Document                                        | Status          | Last Updated | Notes                                |
| ----------------------------------------------- | --------------- | ------------ | ------------------------------------ |
| **DATABASE_PER_TENANT_ARCHITECTURE.md**         | ✅ Production   | Jan 2025     | CRITICAL - Core architecture         |
| **AUTOMATIC_DATABASE_MIGRATION.md**             | ✅ Production   | Oct 2025     | NEW - Auto tenant DB setup           |
| **SHARED_IDENTITY_SERVICE_GUIDE.md**            | ✅ Production   | Jan 2025     | Complete with Tenant Service         |
| **NEW_SERVICE_INTEGRATION_GUIDE.md**            | ✅ Production   | Oct 2024     | Comprehensive guide                  |
| **MULTI_TENANCY_GUIDE.md**                      | ✅ Production   | Oct 2024     | Complete implementation              |
| **FILE_MANAGER_SERVICE_GUIDE.md**               | ✅ Production   | Nov 2025     | Complete with caching & static files |
| **FILE_MANAGER_QUICK_REFERENCE.md**             | ✅ Production   | Nov 2025     | API reference & examples             |
| **NOTIFICATION_SERVICE_README.md**              | ✅ Production   | Nov 2025     | Complete notification guide          |
| **DATABASE_REPLICATION_SETUP_GUIDE.md**         | ✅ Production   | Nov 2025     | NEW - PostgreSQL HA replication      |
| **BOTTLENECKS_COMPLETION_SUMMARY.md**           | ✅ Production   | Nov 2025     | NEW - 11 performance bottlenecks     |
| **PARALLEL_PROCESSING_OPTIMIZATION_SUMMARY.md** | ✅ Production   | Nov 2025     | NEW - 2-50x speedup multi-tenant ops |
| **PERFORMANCE_OPTIMIZATION_GUIDE.md**           | ✅ Production   | Nov 2025     | Complete optimization guide          |
| **DEVICE_TOKEN_REFACTORING_SUMMARY.md**         | ✅ Production   | Nov 2025     | NEW - Device token refactoring       |
| **DEVICE_TOKEN_MANAGEMENT_GUIDE.md**            | ✅ Production   | Nov 2025     | NEW - Device token dev guide         |
| **DEVICE_TOKEN_QUICK_REFERENCE.md**             | ✅ Production   | Nov 2025     | NEW - Device token API ref           |
| **PROJECT_ISOLATION_STRATEGY_GUIDE.md**         | ⚠️ Needs Update | Oct 2024     | Update for multi-DB pattern          |
| **CACHING_STRATEGY_COMPARISON.md**              | ✅ Production   | Oct 2024     | Performance guide                    |
| **TENANT_MIDDLEWARE_EXPLAINED.md**              | ✅ Production   | Oct 2024     | Implementation details               |
| **Testing Docs**                                | ✅ Production   | Oct 2024     | Complete testing suite               |

---

## 🚀 Quick Start Commands

### **Run Identity Service**

```bash
cd src/Services/Identity/Identity.API
dotnet run
# Runs on: https://localhost:5001
```

### **Run Tenant Service**

```bash
cd src/Services/Tenant/Tenant.API
dotnet run
# Runs on: https://localhost:5002
```

### **Run All Services (Docker Compose)**

```bash
docker-compose up -d
```

### **Run Tests**

```bash
# Run all tests
dotnet test

# Run specific service tests
cd src/Services/Identity/Identity.API.Tests
dotnet test
```

---

## 🔑 Key Configuration Values

### **JWT Configuration (All Services - MUST BE IDENTICAL)**

```json
{
  "Jwt": {
    "Secret": "<SAME_SECRET_FOR_ALL_SERVICES>",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp"
  }
}
```

### **Multi-Tenancy Configuration (Optional per Service)**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  }
}
```

---

## 📞 Support & Resources

### **Getting Help**

**Issue Tracking:**

- Create GitHub issue for bugs
- Tag with appropriate label (authentication, multi-tenancy, etc.)

**Documentation Issues:**

- Found outdated info? Submit PR or create issue
- Missing documentation? Request in issues

### **Key Files in Codebase**

| Path                                         | Purpose                                      |
| -------------------------------------------- | -------------------------------------------- |
| `src/Shared/IhsanDev.Shared.Infrastructure/` | Shared infrastructure (middleware, services) |
| `src/Shared/IhsanDev.Shared.Kernel/`         | Domain models & interfaces                   |
| `src/Shared/IhsanDev.Shared.Testing/`        | Testing helpers                              |
| `src/Services/Identity/`                     | Identity service implementation              |
| `src/Services/Tenant/`                       | Tenant service implementation                |

---

## 🎯 Quick Decision Tree

```
Need to...?
│
├─ Create new service?
│  └─ → NEW_SERVICE_INTEGRATION_GUIDE.md
│
├─ Understand architecture?
│  └─ → DATABASE_PER_TENANT_ARCHITECTURE.md
│
├─ Add authentication?
│  └─ → SHARED_IDENTITY_SERVICE_GUIDE.md
│
├─ Enable multi-tenancy?
│  ├─ Quick: MULTI_TENANCY_QUICK_START.md
│  └─ Complete: MULTI_TENANCY_GUIDE.md
│
├─ Handle files?
│  └─ → FILE_MANAGER_SERVICE_GUIDE.md
│
├─ Improve performance?
│  └─ → CACHING_STRATEGY_COMPARISON.md
│
├─ Write tests?
│  └─ → SHARED_TESTING_FILES.md
│
└─ Deploy to production?
   └─ → MULTI_TENANT_DEPLOYMENT_GUIDE.md
```

---

## 📝 Version History

| Version | Date     | Changes                                                                                                                                                                                                                                     |
| ------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2.3     | Nov 2025 | ✅ File Manager Service v2.0.0 complete<br>✅ Redis caching for tenant configs (7-day TTL)<br>✅ Static file serving with public URLs<br>✅ Path/URL separation in responses<br>✅ Improved error handling (404 for missing files)          |
| 2.2     | Nov 2025 | ✅ Documentation cleanup (removed 15 outdated files)<br>✅ Removed temporary implementation summaries<br>✅ Removed bug fix documentation<br>✅ Kept 53 production-ready documents<br>✅ All services verified and up-to-date               |
| 2.1     | Nov 2025 | ✅ Added DATABASE_REPLICATION_SETUP_GUIDE.md<br>✅ Added BOTTLENECKS_COMPLETION_SUMMARY.md<br>✅ Completed all 10 performance bottlenecks<br>✅ Updated NOTIFICATION_SERVICE_README.md<br>✅ Service now supports 100,000+ concurrent users |
| 2.0     | Jan 2025 | ✅ Added DATABASE_PER_TENANT_ARCHITECTURE.md<br>✅ Updated SHARED_IDENTITY_SERVICE_GUIDE.md<br>✅ Consolidated documentation<br>✅ Removed redundant files                                                                                  |
| 1.0     | Oct 2024 | ✅ Initial documentation<br>✅ Multi-tenancy guides<br>✅ Testing infrastructure                                                                                                                                                            |

---

## ✅ Documentation Checklist

Before starting development:

- [ ] Read DATABASE_PER_TENANT_ARCHITECTURE.md
- [ ] Understand multi-database pattern
- [ ] Read SHARED_IDENTITY_SERVICE_GUIDE.md
- [ ] Understand JWT authentication
- [ ] Review NEW_SERVICE_INTEGRATION_GUIDE.md
- [ ] Set up local development environment

Before deploying to production:

- [ ] Review MULTI_TENANT_DEPLOYMENT_GUIDE.md
- [ ] Set up proper JWT secrets (environment variables)
- [ ] Configure database connections per tenant
- [ ] Test tenant isolation
- [ ] Set up monitoring and logging
- [ ] Review CACHING_STRATEGY_COMPARISON.md

---

**Built with ❤️ for Clean Architecture**

_For questions or issues, check the relevant guide or create a GitHub issue._
