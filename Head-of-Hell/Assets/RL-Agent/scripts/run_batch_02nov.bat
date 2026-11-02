@echo off
REM Thesis 02/11: ablation "human-only" (thetaH, theta6h2r0) + missing evals of theta6h2. About 1 hour. Runs from its own folder.
cd /d "%~dp0"
set EXE=C:\Users\arish\Documents\DIENAMO\IL_Mult_Scene\HoH.exe
echo [1/9] Ghost human thetaH (run 1)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_thetaH --n 4 --ilmodel ..\models\thetaH_joint.json --merge
echo [2/9] Ghost human thetaH (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_thetaH_rep --n 4 --ilmodel ..\models\thetaH_joint.json --merge
echo [3/9] Ghost human theta6h2r0 (run 1)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta6h2r0 --n 4 --ilmodel ..\models\theta6h2r0_joint.json --merge
echo [4/9] Ghost human theta6h2r0 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta6h2r0_rep --n 4 --ilmodel ..\models\theta6h2r0_joint.json --merge
echo [5/9] Style eval thetaH
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_thetaH --out StyleEval_thetaH --n 4 --ilmodel ..\models\thetaH_joint.json --merge
echo [6/9] Style eval theta6h2r0
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta6h2r0 --out StyleEval_theta6h2r0 --n 4 --ilmodel ..\models\theta6h2r0_joint.json --merge
echo [7/9] Style eval theta6h2 (replication)
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta6h2 --out StyleEval_theta6h2_rep --n 4 --ilmodel ..\models\theta6h2_joint.json --merge
echo [8/9] Convergence theta6h2 (run 1)
python run_parallel.py --exe "%EXE%" --schedule schedule_convergence_theta5 --out Conv_theta6h2 --n 4 --shardmode pair --ilmodel ..\models\theta6h2_joint.json --merge
echo [9/9] Convergence theta6h2 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_convergence_theta5 --out Conv_theta6h2_c2 --n 4 --shardmode pair --ilmodel ..\models\theta6h2_joint.json --merge
echo ALL DONE
pause
