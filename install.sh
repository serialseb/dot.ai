mkdir -p .skillshare/skills
skillshare init --project --discover


rm -rf .skillshare/extras
ln -s .skillshare/skills/_origin/extras .skillshare/extras
cat > .skillshare/config.yaml <<'EOF'

source: github.com/SafeKeepIt/dot.ai

mode: merge
target_naming: standard

targets:
  - claude
  - opencode
  - codex

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

EOF

skillshare install --project
skillshare sync --project --all
skillshare update --project --all --force --prune
