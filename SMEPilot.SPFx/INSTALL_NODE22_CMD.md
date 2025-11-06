# Install Node 22 via CMD - Step by Step

## 🎯 Quick Install via CMD

### **Option 1: Using nvm-windows (CMD)**

**Open CMD as Administrator:**

```cmd
REM Check if nvm is available
nvm version

REM Install Node 22
nvm install 22

REM Use Node 22
nvm use 22

REM Verify
node --version
npm --version
```

**If nvm activation fails** (due to spaces in username), manually set PATH:

```cmd
REM Set PATH for current CMD session
set PATH=C:\Users\Vinod Kumar\AppData\Local\nvm\v22.21.1;%PATH%
set PATH=C:\Users\Vinod Kumar\AppData\Local\nvm\v22.21.1\node_modules\npm\bin;%PATH%

REM Verify
node --version
```

---

### **Option 2: Download and Install via CMD**

**Download Node 22 installer:**

```cmd
REM Create temp folder
mkdir C:\temp
cd C:\temp

REM Download Node 22 LTS (64-bit)
REM Option A: Using PowerShell from CMD
powershell -Command "Invoke-WebRequest -Uri 'https://nodejs.org/dist/v22.11.0/node-v22.11.0-x64.msi' -OutFile 'node-v22.11.0-x64.msi'"

REM Install silently
msiexec /i node-v22.11.0-x64.msi /quiet /norestart

REM Wait a moment, then verify
timeout /t 5
node --version
```

---

### **Option 3: Using Chocolatey (if installed)**

```cmd
REM Check if chocolatey is installed
choco --version

REM Install Node 22
choco install nodejs-lts --version=22.11.0 -y

REM Verify
node --version
```

---

### **Option 4: Manual Download + CMD Install**

1. **Download manually:**
   - Go to: https://nodejs.org/dist/v22.11.0/
   - Download: `node-v22.11.0-x64.msi`
   - Save to: `C:\temp\node-v22.11.0-x64.msi`

2. **Install via CMD:**
```cmd
cd C:\temp
msiexec /i node-v22.11.0-x64.msi /quiet /norestart
timeout /t 10
node --version
```

---

## ✅ Recommended: Option 1 (nvm-windows)

**If Node 22 is already installed via nvm:**

```cmd
REM Open CMD
REM Navigate to project
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx

REM Set PATH manually (workaround for spaces in username)
set PATH=C:\Users\Vinod Kumar\AppData\Local\nvm\v22.21.1;%PATH%
set PATH=C:\Users\Vinod Kumar\AppData\Local\nvm\v22.21.1\node_modules\npm\bin;%PATH%

REM Verify
node --version
npm --version

REM Then proceed with SPFx upgrade
npm install
npm run build
```

---

## 🚀 After Node 22 is Active

Once `node --version` shows `v22.x.x`:

1. **I'll upgrade SPFx** to 1.21.1
2. **Update React** to 18.2.0  
3. **Fix webpack** properly
4. **Get release folder** populated!

---

## 📝 Quick Test

```cmd
REM Test Node 22 is working
node --version
npm --version

REM Should show:
REM v22.x.x
REM 10.x.x
```

**Ready!** Once Node 22 is active, let me know and I'll complete the SPFx upgrade! 🎯

