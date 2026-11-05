@echo off
REM Thesis 03/11: evals of the RL pilot RLp_b005 (models after 10 / 30 / 60 / 92 updates). About 45-50 minutes.
cd /d "%~dp0"
set EXE=C:\Users\arish\Documents\DIENAMO\IL_Mult_Scene\HoH.exe
set RUN=%USERPROFILE%\Documents\My Games\Head of Hell\RL_runs\RLp_b005
echo [1/12] Ghost human RLp_b005 u10
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_RLp_b005_u10 --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_u10.json" --merge
echo [2/12] Ghost human RLp_b005 u10 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_RLp_b005_u10_rep --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_u10.json" --merge
echo [3/12] Style eval RLp_b005 u10 (agent only)
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta8_agentonly --out StyleEval_RLp_b005_u10 --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_u10.json" --merge
echo [4/12] Ghost human RLp_b005 u30
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_RLp_b005_u30 --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_u30.json" --merge
echo [5/12] Ghost human RLp_b005 u30 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_RLp_b005_u30_rep --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_u30.json" --merge
echo [6/12] Style eval RLp_b005 u30 (agent only)
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta8_agentonly --out StyleEval_RLp_b005_u30 --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_u30.json" --merge
echo [7/12] Ghost human RLp_b005 u60
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_RLp_b005_u60 --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_u60.json" --merge
echo [8/12] Ghost human RLp_b005 u60 (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_RLp_b005_u60_rep --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_u60.json" --merge
echo [9/12] Style eval RLp_b005 u60 (agent only)
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta8_agentonly --out StyleEval_RLp_b005_u60 --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_u60.json" --merge
echo [10/12] Ghost human RLp_b005 final
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_RLp_b005_final --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_final.json" --merge
echo [11/12] Ghost human RLp_b005 final (run 2)
python run_parallel.py --exe "%EXE%" --schedule schedule_ghost_human --out GhostHuman_RLp_b005_final_rep --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_final.json" --merge
echo [12/12] Style eval RLp_b005 final (agent only)
python run_parallel.py --exe "%EXE%" --schedule schedule_style_eval_theta8_agentonly --out StyleEval_RLp_b005_final --n 4 --ilmodel "%RUN%\thetaR_RLp_b005_final.json" --merge
echo ALL DONE
pause
