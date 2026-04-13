skillshare init --project --targets claude,opencode,codex --mode merge
skillshare install SafeKeepIt/dot.ai --track --name origin
ln -s skills/_origin/extras .skillshare/extras
skillshare extras init agents --target . --mode merge --project
skillshare sync
