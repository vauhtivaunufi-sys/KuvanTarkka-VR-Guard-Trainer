# Git Workflow Guide for KuvanTarkka VR Team

**Team:** 3 people (1 Programmer, 2 Artists)
**Unity version** 6000.2.6f2

---

## Testing Without a Headset (editor only)

**The game is VR only.** Keyboard & mouse exists only so the team can test game logic in the Unity editor without a headset.
It is not compiled into builds, is not shown to the customer, and does not decide how anything works in VR.
Aim, reach and feel are not VR-accurate here - final checks are always done in the headset.

1. Open the project in Unity, open `Assets/Scenes/MainTraining.unity`, press **Play**.
2. No headset connected = the keyboard & mouse test tool starts automatically. Click the Game view to capture the mouse.

| Key | Stands in for | Action |
|-----|---------------|--------|
| Mouse | Head | Look |
| W A S D | Left stick | Walk (Left Shift = faster) |
| E | Right grip | Right hand: pick up / drop, open doors |
| Q | Left grip | Left hand: pick up / drop |
| Left mouse | Right trigger | Use item in right hand (taser, spray) |
| Right mouse | Left trigger | Use item in left hand (shield up / down) |
| R / T | A / B | Right controller buttons |
| F / G | X / Y | Left controller buttons |
| Tab | Left menu | Left menu button |
| F1 | - | Show / hide controls on screen |
| F3 | - | Debug overlays on / off |
| Esc | - | Free the mouse (click the Game view to continue) |

**Game freezes on the first frame / nothing reacts?** In the Console window, turn **Error Pause** OFF. Without a headset the Meta XR plugin logs a "no headset" error at start, and Error Pause stops the game on it.

Headset plugged in but you still want keyboard & mouse? **Tools > KuvanTarkka > Desktop Testing (editor only) > Keyboard and Mouse** (saved only on your computer).
Code: `Assets/Scripts/DevTools/DesktopTesting/`. Game code must never reference it.

---

## Quick Start

### **1. Clone the Repository**

```bash
git clone https://github.com/Kyberturvallisuus-ja-peliteknologiat/Kuvantarkka.git
cd Kuvantarkka
```

### **2. Create Your Branch (DO THIS FIRST)**

**Never work on `main` directly.**

Choose your role and create a branch:

```bash
# Programmer
git checkout -b feature/core-mechanics

# Artist 1 (Models & Animations)
git checkout -b feature/npc-models

# Artist 2 (UI)
git checkout -b feature/ui

```

### **3. Work on Your Files**

Make changes to your files (code, models, etc).

### **4. Commit and Push**

```bash
# See what changed
git status

# Add all changes
git add .

# Commit with a message
git commit -m "Add Taser weapon system" 
# or "Add NPC model with animations"
# or "Add UI layouts"
# or "Add taser sound effects"

# Push to your branch (NOT main!)
git push origin feature/your-branch-name
```

---

## Daily Workflow

### **Every Morning: Sync with Team**

```bash
# Get latest changes from main
git pull origin main

# If there are conflicts, resolve them
# (ask programmer for help if unsure)
```

### **During the Day: Commit Often**

```bash
# Every 1-2 hours
git add .
git commit -m "Descriptive message"
git push origin feature/your-branch
```

### **When Your Work is Done: Pull Request**

Go to GitHub:
```
https://github.com/Kyberturvallisuus-ja-peliteknologiat/Kuvantarkka
├─ Pull Requests tab
├─ New Pull Request
├─ From: feature/your-branch
├─ To: main
├─ Write description of what you did
├─ Assign someone to review
└─ Create Pull Request
```

**Wait for review. Programmer will approve and merge.**

---

## Branch Structure

```
main (always stable, ready to show)
├─ feature/core-mechanics (Programmer)
├─ feature/npc-models (Artist)
├─ feature/ui (Artist)
└─ feature/audio (Sound Designer)
```

---

## What Goes Where

| Role | Folder | Branch |
|------|--------|--------|
| **Programmer (Ivan)** | Assets/Scripts/ | feature/core-mechanics |
| **Artist 1** | Assets/Models/ + Assets/Animations/ | feature/npc-models |
| **Artist 2** | Assets/UI/ + Assets/Prefabs/ | feature/ui |

---

## Common Commands

### **Check Status**
```bash
git status
```

### **See Your Changes**
```bash
git diff
```

### **Sync Before Starting Work**
```bash
git pull origin main
```

### **Push Your Changes**
```bash
git add .
git commit -m "Your message"
git push origin feature/your-branch
```

### **Switch Between Branches**
```bash
git checkout feature/npc-models
git checkout main
git checkout feature/your-branch
```

### **View All Branches**
```bash
git branch -a
```

---

## Common Issues & Solutions

### **Problem: "You are not currently on a branch"**

```bash
git checkout -b feature/your-branch
```

### **Problem: Files Say "Modified" but You Didn't Touch Them**

```bash
git pull origin main
```

### **Problem: Merge Conflict**

1. Tell the Programmer
2. Programmer fixes it
3. Continue working

### **Problem: "Permission Denied" when Pushing**

1. Check you have write access to repo
2. Check SSH keys are set up correctly
3. Ask repository admin for access

---

## Rules (Important!)

✅ **DO:**
- Create a branch for your work
- Commit frequently (every 1-2 hours)
- Write clear commit messages
- Pull from main every morning
- Push to your branch (not main!)
- Use Pull Requests for merging

❌ **DON'T:**
- Push directly to main
- Commit huge changes at once
- Use unclear messages like "fix stuff"
- Merge your own Pull Requests
- Commit files you don't understand

---

## Git Workflow Diagram

```
You Start
   ↓
Create branch: git checkout -b feature/your-feature
   ↓
Make changes to your files
   ↓
git add . && git commit -m "message"
   ↓
git push origin feature/your-feature
   ↓
Repeat: make changes, commit, push
   ↓
Work done?
   ↓
Create Pull Request on GitHub
   ↓
Programmer reviews
   ↓
Merged to main ✓
   ↓
Everyone pulls: git pull origin main
```

---

## Example: Day 1 for Each Role

### **Programmer (Ivan)**
```bash
git checkout -b feature/core-mechanics
# Create GameManager.cs, PlayerController.cs
git add .
git commit -m "Add XR core systems"
git push origin feature/core-mechanics
```

### **Artist 1 (Models)**
```bash
git checkout -b feature/npc-models
# Add prisoner.fbx, animations
git add Assets/Models/
git commit -m "Add NPC model with walk/idle animations"
git push origin feature/npc-models
```

### **Artist 2 (UI)**
```bash
git checkout -b feature/ui
# Create UI prefabs
git add Assets/UI/
git commit -m "Add objective and health UI layouts"
git push origin feature/ui
```

---


## Useful GitHub Features

**Projects tab** → Track progress (Kanban board)
**Issues tab** → Report bugs or create tasks
**Pull Requests tab** → See ongoing work
**Discussions tab** → Team chat
