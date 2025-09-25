#!/bin/bash
# Setup Git hooks for this repo

git config core.hooksPath .githooks
echo "Git hooks path set to .githooks"
echo "✅ Pre-commit and commit-msg hooks are now active."
