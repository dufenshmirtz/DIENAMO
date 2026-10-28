@echo off
REM Thesis 27/10: unattended batch (~1 h). Run from any folder: it switches to this scripts folder itself.
cd /d "%~dp0"
set EXE=C:\Users\arish\Documents\DIENAMO\IL_Mult_Scene\HoH.exe
echo [1/7] Convergence theta6 (run 1)
python run_parallel.py --exe "%EXE%" --schedule schedule_convergence_theta5 --out Conv_theta6 --n 4 --shardmode pair --ilmodel ..\models\theta6_joint.json --merge
echo [2/7] Convergence theta6 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_convergence_theta5 --out Conv_theta6_c2 --n 4 --shardmode pair --ilmodel ..\models\theta6_joint.json --merge
echo [3/7] Convergence theta6h (run 1)
python run_parallel.py --exe "%EXE%" --schedule schedule_convergence_theta5 --out Conv_theta6h --n 4 --shardmode pair --ilmodel ..\models\theta6h_joint.json --merge
echo [4/7] Convergence theta6h (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_convergence_theta5 --out Conv_theta6h_c2 --n 4 --shardmode pair --ilmodel ..\models\theta6h_joint.json --merge
echo [5/7] Style eval theta6h (replication)
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta6h --out StyleEval_theta6h_rep --n 4 --ilmodel ..\models\theta6h_joint.json --merge
echo [6/7] Ghost human theta6h (replication)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta6h_rep --n 4 --ilmodel ..\models\theta6h_joint.json --merge
echo [7/7] Ghost human theta6 (replication)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta6_rep --n 4 --ilmodel ..\models\theta6_joint.json --merge
echo ALL DONE
pause
