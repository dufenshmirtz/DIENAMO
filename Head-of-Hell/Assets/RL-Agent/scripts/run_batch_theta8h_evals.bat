@echo off
REM Thesis 02/11: in-game evals of theta8h (q=0 and q=0.5 history dropout). About 30-35 minutes. Runs from its own folder.
cd /d "%~dp0"
set EXE=C:\Users\arish\Documents\DIENAMO\IL_Mult_Scene\HoH.exe
echo [1/8] Style eval theta8h q0 (agent + bot v6.1 teachers)
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta8 --out StyleEval_theta8h_q0 --n 4 --ilmodel ..\models\theta8h_q0_joint.json --merge
echo [2/8] Style eval theta8h q05 (agent only)
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta8_agentonly --out StyleEval_theta8h_q05 --n 4 --ilmodel ..\models\theta8h_q05_joint.json --merge
echo [3/8] Ghost human theta8h q0
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta8h_q0 --n 4 --ilmodel ..\models\theta8h_q0_joint.json --merge
echo [4/8] Ghost human theta8h q0 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta8h_q0_rep --n 4 --ilmodel ..\models\theta8h_q0_joint.json --merge
echo [5/8] Ghost human theta8h q05
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta8h_q05 --n 4 --ilmodel ..\models\theta8h_q05_joint.json --merge
echo [6/8] Ghost human theta8h q05 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta8h_q05_rep --n 4 --ilmodel ..\models\theta8h_q05_joint.json --merge
echo [7/8] Pressure v6 theta8h q0
python run_parallel.py --exe "%EXE%" --schedule schedule_pressure_v6 --out Pressure6_theta8h_q0 --n 4 --ilmodel ..\models\theta8h_q0_joint.json --merge
echo [8/8] Pressure v6 theta8h q05
python run_parallel.py --exe "%EXE%" --schedule schedule_pressure_v6 --out Pressure6_theta8h_q05 --n 4 --ilmodel ..\models\theta8h_q05_joint.json --merge
echo ALL DONE
pause
