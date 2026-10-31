import json, matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
D = json.load(open("profile_cmp.json"))
S, B = D["style"], D["behaviour"]
series = [("Aris (natural, 70 rounds)", "#2a78d6"), ("θ6h mirror (30 rounds)", "#eb6834"),
          ("θ5 mirror (8 rounds)", "#1baf7a"), ("REF scripted bot", "#9b9a95")]
style_vals = {
    series[0][0]: S["Aris P00N (70 rounds vs REF)"]["aris"],
    series[1][0]: S["θ6h mirror (28-30/10)"]["agent"],
    series[2][0]: S["θ5 mirror (19/10)"]["agent"],
    series[3][0]: S["REF (vs Aris)"]["agent"]}
bkey = {series[0][0]: "Aris P00N", series[1][0]: "θ6h mirror", series[2][0]: "θ5 mirror", series[3][0]: "REF"}
panels = [("Attack rate", lambda s: style_vals[s][0], "{:.2f}"),
          ("Mobility rate", lambda s: style_vals[s][1], "{:.2f}"),
          ("Defense rate", lambda s: style_vals[s][2], "{:.3f}"),
          ("Mobility-time ratio", lambda s: style_vals[s][3], "{:.2f}"),
          ("Block-time ratio", lambda s: style_vals[s][4], "{:.3f}"),
          ("Attacks per second", lambda s: B[bkey[s]]["r_attack"], "{:.2f}"),
          ("Next attack after a hit (s)", lambda s: B[bkey[s]]["after_hit"], "{:.2f}"),
          ("Block segment, median (s)", lambda s: B[bkey[s]]["block_med"], "{:.2f}"),
          ("Heavy share of damage", lambda s: B[bkey[s]]["dmg_Heavy"], "{:.2f}")]
INK, INK2, GRID, SURF = "#0b0b0b", "#52514e", "#e6e5e1", "#fcfcfb"
plt.rcParams.update({"font.family": "DejaVu Sans", "font.size": 9, "axes.edgecolor": GRID, "text.color": INK})
fig, axs = plt.subplots(3, 3, figsize=(10, 7.2), facecolor=SURF)
for ax, (title, f, fmt) in zip(axs.flat, panels):
    vals = [f(s) for s, _ in series]
    ys = list(range(len(series)))[::-1]
    ax.barh(ys, vals, height=0.62, color=[c for _, c in series], edgecolor=SURF, linewidth=2)
    m = max(vals)
    for y, v in zip(ys, vals):
        ax.text(v + m * 0.02, y, fmt.format(v), va="center", fontsize=8, color=INK2)
    ax.set_xlim(0, m * 1.25); ax.set_yticks([]); ax.set_facecolor(SURF)
    ax.set_title(title, fontsize=9.5, loc="left", color=INK)
    ax.tick_params(axis="x", colors=INK2, labelsize=7.5, length=0)
    ax.grid(axis="x", color=GRID, linewidth=0.6); ax.set_axisbelow(True)
    for sp in ("top", "right", "left"): ax.spines[sp].set_visible(False)
handles = [plt.Rectangle((0, 0), 1, 1, color=c) for _, c in series]
fig.legend(handles, [n for n, _ in series], loc="upper center", ncol=4, frameon=False, fontsize=9, bbox_to_anchor=(0.5, 0.965))
fig.suptitle("Play profile: Aris vs the IL agents that mirror him (Skipler mirror, Classic)", x=0.01, ha="left", fontsize=11.5, y=0.995)
fig.text(0.01, 0.005, "Style axes (top two rows, first five panels) are the 5-axis style vector the agent is conditioned on; the other panels are event metrics from telemetry. "
         "Aris and REF: Aris's natural rounds vs REF; mirrors: the agent's rounds in the mirror sessions vs Aris.", fontsize=7, color=INK2, wrap=True)
fig.tight_layout(rect=(0, 0.03, 1, 0.93))
fig.savefig("profile_aris_vs_agents.png", dpi=200, facecolor=SURF)
print("ok")
