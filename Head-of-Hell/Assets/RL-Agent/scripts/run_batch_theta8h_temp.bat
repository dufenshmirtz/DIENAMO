@echo off
REM Thesis 02/11: theta8h longer fine-tune (e60) and temperature 0.8 / 0.6 (baked into the model). About 35 minutes.
cd /d "%~dp0"
set EXE=C:\Users\arish\Documents\DIENAMO\IL_Mult_Scene\HoH.exe
echo [1/9] Ghost human theta8h q0_e60
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta8h_q0_e60 --n 4 --ilmodel ..\models\theta8h_q0_e60_joint.json --merge
echo [2/9] Ghost human theta8h q0_e60 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta8h_q0_e60_rep --n 4 --ilmodel ..\models\theta8h_q0_e60_joint.json --merge
echo [3/9] Style eval theta8h q0_e60 (agent only)
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta8_agentonly --out StyleEval_theta8h_q0_e60 --n 4 --ilmodel ..\models\theta8h_q0_e60_joint.json --merge
echo [4/9] Ghost human theta8h e60_T08
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta8h_e60_T08 --n 4 --ilmodel ..\models\theta8h_e60_T08_joint.json --merge
echo [5/9] Ghost human theta8h e60_T08 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta8h_e60_T08_rep --n 4 --ilmodel ..\models\theta8h_e60_T08_joint.json --merge
echo [6/9] Style eval theta8h e60_T08 (agent only)
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta8_agentonly --out StyleEval_theta8h_e60_T08 --n 4 --ilmodel ..\models\theta8h_e60_T08_joint.json --merge
echo [7/9] Ghost human theta8h e60_T06
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta8h_e60_T06 --n 4 --ilmodel ..\models\theta8h_e60_T06_joint.json --merge
echo [8/9] Ghost human theta8h e60_T06 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_theta8h_e60_T06_rep --n 4 --ilmodel ..\models\theta8h_e60_T06_joint.json --merge
echo [9/9] Style eval theta8h e60_T06 (agent only)
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta8_agentonly --out StyleEval_theta8h_e60_T06 --n 4 --ilmodel ..\models\theta8h_e60_T06_joint.json --merge
echo ALL DONE
pause
