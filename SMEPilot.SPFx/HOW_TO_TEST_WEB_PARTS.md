# How to Test Your Web Parts - Simple Guide

## ✅ What You've Done So Far
1. ✅ Workbench is open at: `onblick.sharepoint.com/sites/DocEnricher-PoC/_layouts/15/workbench.aspx`
2. ✅ Development server is running on port 4321
3. ✅ You clicked the "+" icon and see the "Add a new web part" popup

## 🔍 Where to Find Your Web Parts

In the "Add a new web part" popup, scroll down and look for:

### Option 1: Look for "Advanced" Category
Your web parts are in the **"Advanced"** category (not "Local" or "AI").

Look for:
- **"SMEPilot Document Upload"** 
- **"SMEPilot Admin"**

### Option 2: Search for Them
1. Click in the **Search box** at the top of the popup
2. Type: `SMEPilot`
3. Your web parts should appear

## 🐛 If You Don't See Them

### Step 1: Check Browser Console
1. Press **F12** to open Developer Tools
2. Click the **Console** tab
3. Look for any **red errors**
4. Take a screenshot and share it

### Step 2: Refresh the Page
1. Press **Ctrl + F5** (hard refresh)
2. Wait for page to reload
3. Click "+" again

### Step 3: Check Terminal
Look at the terminal where `npx gulp serve` is running:
- Are there any **errors**?
- Does it say "**Finished subtask 'serve'**"?
- Share any error messages you see

## 📋 Quick Checklist

- [ ] `npx gulp serve` is running (✅ Yes, port 4321 is active)
- [ ] Workbench page is open (✅ Yes, you're there)
- [ ] Clicked "+" icon (✅ Yes, popup is open)
- [ ] Scrolled through all categories in popup (❓ Need to check)
- [ ] Searched for "SMEPilot" (❓ Need to check)
- [ ] Checked browser console for errors (❓ Need to check)

## 🎯 Next Steps

1. **Scroll down** in the "Add a new web part" popup
2. Look for **"Advanced"** category
3. Or **search** for "SMEPilot"
4. If still not found, check **browser console (F12)** for errors

