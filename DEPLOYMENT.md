# 🚀 Complete Production Deployment Guide

## StarPlus POS Billing System - Render Deployment

> **Production Stack**: React + .NET 9 + PostgreSQL on Render  
> **Cost**: Free tier available (Backend + Database upgradeable to $5/month for production)

### ⚠️ If you see "Failed to load customer data" / "column s.RoundOff does not exist" / "Failed to load expense categories"

Your **production database is missing schema**.

**Easiest fix:** **Redeploy the backend** on Render. The app now runs `ApplyMissingSchema.sql` automatically on startup when using PostgreSQL, so one redeploy should add the missing columns/tables and fix the errors.

**If redeploy doesn’t fix it**, run the script manually once:
1. Open **[Render Dashboard](https://dashboard.render.com/)** → select your **PostgreSQL** service.
2. Click **Connect** (or use the **PSQL Command** / **External Database URL**).
3. Copy the **entire contents** of `backend/FrozenApi/Scripts/ApplyMissingSchema.sql` from this repo and paste into the PSQL session, then run it.
4. Reload your app.

(See [Issue 6: Schema outdated](#issue-6-schema-outdated-roundoff--missing-column) in Troubleshooting for details.)

---

## 📋 Table of Contents
1. [Prerequisites](#prerequisites)
2. [GitHub Setup](#github-setup)
3. [Render PostgreSQL Setup](#render-postgresql-setup)
4. [Backend API Deployment](#backend-api-deployment)
5. [Frontend Deployment (Netlify)](#frontend-deployment-netlify)
6. [Environment Configuration](#environment-configuration)
7. [Auto-Deployment from GitHub](#auto-deployment-from-github)
8. [Fresh Client Setup](#fresh-client-setup)
9. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Accounts
- ✅ GitHub account (free)
- ✅ Render.com account (free tier available)
- ✅ Netlify account (free tier available)

### Local Requirements
- Git installed
- Node.js v18+ (for local testing)
- .NET SDK 9.0 (for local testing)

---

## GitHub Setup

### 1. Create GitHub Repository

```bash
# Initialize git (if not already done)
cd /path/to/Starplus-Billing_App-Finalized-plsql
git init

# Add all files
git add .

# Commit
git commit -m "Initial commit - Production ready PostgreSQL only"

# Create repository on GitHub, then:
git remote add origin https://github.com/YOUR-USERNAME/starplus-billing.git
git branch -M main
git push -u origin main
```

### 2. Verify .gitignore

Ensure these are in `.gitignore`:
```
**/node_modules/
**/bin/
**/obj/
**/.env
**/.env.local
**/appsettings.Development.json
*.db
*.db-*
```

---

## Render PostgreSQL Setup

### 1. Create Database

1. Go to [Render Dashboard](https://dashboard.render.com/)
2. Click **"New +"** → **"PostgreSQL"**
3. Configure:
   - **Name**: `starplus-db`
   - **Database**: `starplusdb`
   - **Region**: Choose closest to your location
   - **PostgreSQL Version**: 15
   - **Plan**: 
     - **Free** (1GB storage, good for testing)
     - **$7/month** (15GB storage, recommended for production)

4. Click **"Create Database"**

5. **IMPORTANT**: Copy and save these credentials:
   - **Internal Database URL** (use this for backend)
   - **External Database URL** (use for backups/management)
   - Host, Port, Database, Username, Password

### 2. Database Plans Comparison

| Plan | Price | Storage | RAM | Best For |
|------|-------|---------|-----|----------|
| Free | $0 | 1GB | Shared | Testing, Demo |
| Starter | $7 | 15GB | 1GB | Small Business |
| Standard | $20+ | Custom | Custom | Production |

**Recommendation**: Start with **Free** for testing, upgrade to **$7/month** for client deployment.

---

## Backend API Deployment

### 1. Create Web Service on Render

1. Click **"New +"** → **"Web Service"**
2. Connect to your GitHub repository
3. Configure:

**Basic Settings:**
- **Name**: `starplus-backend`
- **Region**: Same as database (important!)
- **Branch**: `main`
- **Root Directory**: `backend/FrozenApi`
- **Environment**: `Docker`
- **Dockerfile Path**: `backend/FrozenApi/Dockerfile`

**Instance Type:**
- **Free** (512MB RAM, sleeps after inactivity)
- **Starter ($7/month)** (512MB RAM, always on - **Recommended for production**)

### 2. Environment Variables

Click **"Add Environment Variable"** and add these exactly:

```bash
# Database Connection (CRITICAL - Use Internal URL from database!)
ConnectionStrings__DefaultConnection=Host=YOUR-DB-INTERNAL-HOST;Port=5432;Database=starplusdb;Username=YOUR-USERNAME;Password=YOUR-PASSWORD;SSL Mode=Require

# Security (CHANGE THESE VALUES!)
JwtSettings__SecretKey=CHANGE-THIS-TO-RANDOM-64-CHARACTER-STRING-abc123XYZ!
JwtSettings__Issuer=FrozenApi
JwtSettings__Audience=FrozenApi
JwtSettings__ExpiryInHours=8

# CORS - Add your frontend URL after deployment
ALLOWED_ORIGINS=https://your-app-name.netlify.app,https://your-custom-domain.com

# ASP.NET Environment
ASPNETCORE_ENVIRONMENT=Production
```

**How to get Internal Database URL:**
1. Go to your database in Render
2. Click **"Connect"**
3. Copy the **Internal Database URL**
4. Format: `dpg-xxxxx-a.oregon-postgres.render.com`

### 3. Deploy

1. Click **"Create Web Service"**
2. Wait 5-10 minutes for deployment
3. Monitor logs for:
   - ✅ "Using PostgreSQL database"
   - ✅ "Database migrations applied successfully"
   - ✅ "Admin user verified - login should work"

### 4. Verify Backend

Test these URLs (replace with your actual URL):

```bash
# Health check
https://starplus-backend.onrender.com/api/cors-check

# Swagger API docs
https://starplus-backend.onrender.com/swagger

# Test login
curl -X POST https://starplus-backend.onrender.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@starplus.com","password":"Admin123!"}'
```

---

## Frontend Deployment (Netlify)

### 1. Prepare Frontend

Update `frontend/frozen-ui/src/config.js` or `.env`:

```bash
# Create .env file in frontend/frozen-ui
VITE_API_URL=https://starplus-backend.onrender.com
```

### 2. Deploy to Netlify

**Option A: Via Netlify Dashboard**

1. Go to [Netlify](https://app.netlify.com/)
2. Click **"Add new site"** → **"Import an existing project"**
3. Connect to GitHub repository
4. Configure:
   - **Base directory**: `frontend/frozen-ui`
   - **Build command**: `npm install && npm run build`
   - **Publish directory**: `dist`
5. **Environment variables**:
   ```
   VITE_API_URL=https://starplus-backend.onrender.com
   ```
6. Click **"Deploy site"**

**Option B: Via CLI**

```bash
cd frontend/frozen-ui

# Install Netlify CLI
npm install -g netlify-cli

# Login
netlify login

# Deploy
netlify deploy --prod
```

### 3. Update CORS on Backend

After frontend deploys:

1. Copy your Netlify URL: `https://your-app.netlify.app`
2. Go to Render → Backend service → **Environment**
3. Update `ALLOWED_ORIGINS`:
   ```
   ALLOWED_ORIGINS=https://your-app.netlify.app
   ```
4. Click **"Save Changes"** (auto-redeploys)

---

## Environment Configuration

### Backend Environment Variables (Complete List)

| Variable | Example | Required | Notes |
|----------|---------|----------|-------|
| `ConnectionStrings__DefaultConnection` | `Host=db-host;Port=5432;...` | ✅ | Use Internal URL |
| `JwtSettings__SecretKey` | `64+char random string` | ✅ | MUST be changed! |
| `JwtSettings__Issuer` | `FrozenApi` | ✅ | Keep as is |
| `JwtSettings__Audience` | `FrozenApi` | ✅ | Keep as is |
| `JwtSettings__ExpiryInHours` | `8` | ✅ | Token lifetime |
| `ALLOWED_ORIGINS` | `https://app.netlify.app` | ✅ | Frontend URL |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ✅ | Required |

### Frontend Environment Variables

| Variable | Example | Required |
|----------|---------|----------|
| `VITE_API_URL` | `https://backend.onrender.com` | ✅ |

---

## Auto-Deployment from GitHub

### How It Works

Both Render and Netlify support **automatic deployment** when you push to GitHub:

1. **Make changes** locally
2. **Commit and push** to GitHub:
   ```bash
   git add .
   git commit -m "Add new feature"
   git push origin main
   ```
3. **Automatic deployment** triggers:
   - Render rebuilds backend (5-10 min)
   - Netlify rebuilds frontend (2-5 min)

### Configure Auto-Deploy

**Render Backend:**
- Go to Service → **Settings** → **Build & Deploy**
- Ensure **"Auto-Deploy"** is **enabled** for `main` branch

**Netlify Frontend:**
- Go to Site → **Site settings** → **Build & deploy**
- Ensure **"Deploy status: Active"**

### Manual Deploy (if needed)

**Render:**
- Go to Service → **Manual Deploy** → Click **"Deploy latest commit"**

**Netlify:**
- Go to **Deploys** → Click **"Trigger deploy"** → **"Deploy site"**

---

## Fresh Client Setup

### Scenario: Give client a fresh app with no data

Your client wants to test the system from scratch without any pre-existing data.

### Steps:

#### 1. Reset Database (Fresh Start)

**Option A: Via Render Dashboard**
```bash
# From Render Database Shell
psql $DATABASE_URL

# Drop all tables and recreate
DROP SCHEMA public CASCADE;
CREATE SCHEMA public;
GRANT ALL ON SCHEMA public TO postgres;
GRANT ALL ON SCHEMA public TO public;
\q
```

**Option B: Via Backend API**
```bash
# Use the Reset endpoint (Admin only)
curl -X POST https://starplus-backend.onrender.com/api/reset/database \
  -H "Authorization: Bearer YOUR_ADMIN_TOKEN"
```

#### 2. Redeploy Backend

Render → Backend service → **Manual Deploy** → **"Clear build cache & deploy"**

This will:
- ✅ Run all migrations
- ✅ Create fresh tables
- ✅ Seed admin/staff users
- ✅ Empty database ready for client data

#### 3. Client Login Credentials

Provide these credentials to your client:

```
Frontend URL: https://your-app.netlify.app
Admin Email:  admin@starplus.com
Admin Password: Admin123!

Staff Email: staff@starplus.com
Staff Password: Staff123!
```

**⚠️ IMPORTANT**: Tell client to change passwords immediately after first login!

#### 4. First-Time Setup Checklist for Client

- [ ] Login with admin credentials
- [ ] Change admin password
- [ ] Add company logo (Settings → Company)
- [ ] Configure invoice template (Settings → Invoice)
- [ ] Add initial products (Products → Import or Add)
- [ ] Add customers (Customers → Add)
- [ ] Test creating a sale
- [ ] Test generating PDF invoice

---

## Troubleshooting

### Issue 1: Backend Connection Errors

**Symptom:**
```
Cannot connect to PostgreSQL
password authentication failed
```

**Solutions:**
1. ✅ Verify `ConnectionStrings__DefaultConnection` is correct
2. ✅ Use **Internal** database URL (not External)
3. ✅ Include `SSL Mode=Require`
4. ✅ Check database and backend are in same region

### Issue 2: Frontend Can't Connect

**Symptom:** CORS errors in browser console

**Solutions:**
1. ✅ Check `ALLOWED_ORIGINS` includes frontend URL
2. ✅ No trailing slash in URL: `https://app.netlify.app` ✅ `https://app.netlify.app/` ❌
3. ✅ Redeploy backend after changing CORS

### Issue 3: Database Not Migrating

**Symptom:** "Table does not exist" errors

**Solutions:**
1. Check logs: Should see "Database migrations applied successfully"
2. Manually run migrations via Render Shell:
   ```bash
   cd backend/FrozenApi
   dotnet ef database update
   ```
3. Or trigger full redeploy: **Clear build cache & deploy**

### Issue 4: Free Tier Backend Sleeps

**Symptom:** First request takes 30+ seconds

**Solution:**
- This is normal on Free tier
- Upgrade to Starter ($7/month) for always-on backend
- Or use a ping service (e.g., UptimeRobot) to keep it awake

### Issue 5: Login Not Working

**Symptom:** Invalid credentials

**Solutions:**
1. Check logs: Look for "Admin user verified"
2. Verify database has Users table:
   ```bash
   psql $DATABASE_URL -c "SELECT * FROM \"Users\""
   ```
3. If missing, redeploy to re-run migrations

### Issue 6: Schema outdated (RoundOff / missing column)

**Symptom:** API errors such as:
- `42703: column s.RoundOff does not exist`
- "Failed to load customer data" or "Failed to load expense categories"
- `DbUpdateException` or missing-column / `errorMissingColumn` in reports, customer ledger, or expenses

**Cause:** The production database is behind the app schema (migrations were not applied—e.g. `__EFMigrationsHistory` out of sync or app never ran `Database.Migrate()`).

**Solution:** Run the one-time schema script once against your Render Postgres:

1. Open [Render Dashboard](https://dashboard.render.com/) → your **PostgreSQL** service.
2. Use **Connect** (or the "External Database URL" / "PSQL Command").
3. Run the contents of `backend/FrozenApi/Scripts/ApplyMissingSchema.sql` (e.g. paste into the PSQL session or run `psql $DATABASE_URL -f backend/FrozenApi/Scripts/ApplyMissingSchema.sql` from a shell with the file and URL).
4. Reload the app; customer data, sales report (with RoundOff), expense categories, and payment receipts should load without 42703 or missing-column errors.

After this, normal deploys keep the schema in sync via startup `Database.Migrate()` as long as the web service is the latest code and uses the same `DATABASE_URL`.

---

## Production Checklist

### Before Giving to Client

- [ ] Backend deployed and healthy
- [ ] Database created and migrated
- [ ] Frontend deployed and accessible
- [ ] CORS configured correctly
- [ ] Test login with admin credentials
- [ ] Test creating product, customer, sale
- [ ] Test PDF generation
- [ ] Change `JwtSettings__SecretKey` to unique value
- [ ] Database backed up
- [ ] Client documentation prepared

### Recommended Upgrades for Production

| Service | Free | Recommended | Cost |
|---------|------|-------------|------|
| Backend | 512MB RAM, Sleeps | Starter (Always on) | $7/month |
| Database | 1GB storage | 15GB storage | $7/month |
| Frontend | Free | Free (sufficient) | $0 |
| **Total** | **$0** | **$14/month** | |

---

## Quick Commands Reference

### Git Commands
```bash
# Push changes
git add .
git commit -m "Update feature"
git push origin main

# Check status
git status

# View remote
git remote -v
```

### Database Commands
```bash
# Connect to database
psql $DATABASE_URL

# List tables
\dt

# Check users
SELECT * FROM "Users";

# Exit
\q
```

### Testing Backend
```bash
# Health check
curl https://backend.onrender.com/api/cors-check

# Login
curl -X POST https://backend.onrender.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@starplus.com","password":"Admin123!"}'
```

---

## Support & Maintenance

### Monthly Costs (Recommended Production Setup)
- Backend (Starter): $7
- Database (15GB): $7
- Frontend (Netlify): $0
- **Total**: $14/month

### Backup Strategy
1. Automated backups via Render (included in paid plans)
2. Manual backup via API: `/api/backup/create`
3. Database export: `pg_dump`

### Monitoring
- Check Render logs daily
- Monitor database size
- Test critical features weekly
- Review security settings monthly

---

## 🎉 Deployment Complete!

Your StarPlus POS Billing System is now live in production!

**Next Steps:**
1. ✅ Send client their login credentials
2. ✅ Walk them through first-time setup
3. ✅ Monitor deployment for first few days
4. ✅ Schedule regular backups
5. ✅ Plan for scaling if needed

**Need Help?** Check:
- Render Dashboard logs
- Netlify deploy logs  
- This troubleshooting guide
- [Render Documentation](https://render.com/docs)

---

**Deployed with ❤️ - Production Ready PostgreSQL Stack**
