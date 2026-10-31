@echo off
REM Thesis 30/10: evals of theta6h2 (theta6 + Human_v2), ~15 min. Runs from its own folder.
cd /d "%~dp0"
set EXE=C:\Users\arish\Documents\DIENAMO\IL_Mult_Scene\HoH.exe
echo [1/3] Style eval theta6h2
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta6h2 --out StyleEval_theta6h2 --n 4 --ilmodel ..\models\theta6h2_joint.json --merge
echo [2/3] Ghost human theta6h2 (run 1)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta6h2 --n 4 --ilmodel ..\models\theta6h2_joint.json --merge
echo [3/3] Ghost human theta6h2 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta6h2_rep --n 4 --ilmodel ..\models\theta6h2_joint.json --merge
echo ALL DONE
pause
