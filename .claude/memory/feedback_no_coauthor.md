---
name: No Co-Authored-By in commits
description: Never add Co-Authored-By trailer to git commit messages in any project
type: feedback
---

Never add `Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>` (or any Co-Authored-By line) to git commit messages.

**Why:** User explicitly requested this be removed from all commits across all projects.

**How to apply:** When creating any git commit, omit the Co-Authored-By trailer entirely.
