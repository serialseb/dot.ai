# Clear old state but keep your intended structure
rm -rf .skillshare/skills .skillshare/extras

# Initialize (creates .skillshare/ structure)



# Write your config
cat > .skillshare/config.yaml <<'eof'
source: https://github.com/SafeKeepIt/dot.ai
mode: merge
target_naming: standard
targets: [claude, opencode, codex]
extras:
  - name: agents
    targets:
      - path: .
        mode: merge
        flatten: true
  - name: conf
    targets:
      - path: .skillshare/config.yaml
        mode: merge
        flatten: true
eof

# Pull from Git (This replaces your non-working 'install' step)
skillshare sync --project --all
skillshare update --project --all --force --prune

# Final link (do this AFTER update to ensure _origin is populated)
ln -s .skillshare/skills/_origin/extras .skillshare/extras

# Final sync to push to targets (claude, etc.)
