import os
import threading
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from pathlib import Path
from PIL import Image, ImageTk
from tkinterdnd2 import TkinterDnD, DND_FILES

from compressor import batch_compress, get_file_size_str, SUPPORTED_FORMATS


class App(TkinterDnD.Tk):
    def __init__(self):
        super().__init__()
        self.title("图片压缩工具")
        self.geometry("1100x740")
        self.minsize(860, 600)
        self.configure(bg="#f5f5f5")

        self._input_files = []
        self._cancel_flag = [False]
        self._preview_job = None
        self._log_data = {}       # iid -> log string
        self._show_log = tk.BooleanVar(value=False)

        self._build_ui()

    # ──────────────────────────── UI BUILD ────────────────────────────

    def _build_ui(self):
        # ── Top toolbar ──
        toolbar = tk.Frame(self, bg="#2c2c2c", height=48)
        toolbar.pack(fill="x")
        toolbar.pack_propagate(False)

        tk.Label(toolbar, text="🖼  图片压缩工具", font=("微软雅黑", 14, "bold"),
                 bg="#2c2c2c", fg="white").pack(side="left", padx=16, pady=8)

        tk.Button(toolbar, text="添加图片", command=self._add_files,
                  bg="#4a9eff", fg="white", relief="flat", padx=12,
                  font=("微软雅黑", 10)).pack(side="left", padx=4, pady=8)
        tk.Button(toolbar, text="添加文件夹", command=self._add_folder,
                  bg="#4a9eff", fg="white", relief="flat", padx=12,
                  font=("微软雅黑", 10)).pack(side="left", padx=4, pady=8)
        tk.Button(toolbar, text="清空列表", command=self._clear_list,
                  bg="#666", fg="white", relief="flat", padx=12,
                  font=("微软雅黑", 10)).pack(side="left", padx=4, pady=8)

        # Log toggle button in toolbar
        self._log_btn = tk.Button(
            toolbar, text="显示日志 ▼", command=self._toggle_log,
            bg="#555", fg="white", relief="flat", padx=10,
            font=("微软雅黑", 10)
        )
        self._log_btn.pack(side="left", padx=4, pady=8)

        self._btn_start = tk.Button(toolbar, text="▶ 开始压缩", command=self._start,
                                    bg="#27ae60", fg="white", relief="flat", padx=14,
                                    font=("微软雅黑", 10, "bold"))
        self._btn_start.pack(side="right", padx=8, pady=8)

        self._btn_cancel = tk.Button(toolbar, text="■ 取消", command=self._cancel,
                                     bg="#e74c3c", fg="white", relief="flat", padx=12,
                                     font=("微软雅黑", 10), state="disabled")
        self._btn_cancel.pack(side="right", padx=4, pady=8)

        # ── Main area ──
        self._main_pane = tk.PanedWindow(self, orient="vertical", bg="#f5f5f5",
                                          sashwidth=5, sashrelief="flat")
        self._main_pane.pack(fill="both", expand=True, padx=8, pady=6)

        # Top section: file list + options/preview
        top = tk.PanedWindow(self._main_pane, orient="horizontal", bg="#f5f5f5",
                              sashwidth=5, sashrelief="flat")
        self._main_pane.add(top)

        # Left: file list + progress
        left = tk.Frame(top, bg="#f5f5f5")
        top.add(left, minsize=400)

        tk.Label(left, text="文件列表", font=("微软雅黑", 10, "bold"),
                 bg="#f5f5f5").pack(anchor="w", pady=(0, 2))

        list_frame = tk.Frame(left, bg="#f5f5f5")
        list_frame.pack(fill="both", expand=True)

        cols = ("文件名", "大小", "状态", "压缩率")
        self._tree = ttk.Treeview(list_frame, columns=cols, show="headings",
                                   selectmode="browse")
        self._tree.heading("文件名", text="文件名")
        self._tree.heading("大小", text="原始大小")
        self._tree.heading("状态", text="状态")
        self._tree.heading("压缩率", text="压缩率")
        self._tree.column("文件名", width=220, anchor="w")
        self._tree.column("大小", width=80, anchor="center")
        self._tree.column("状态", width=100, anchor="center")
        self._tree.column("压缩率", width=70, anchor="center")
        self._tree.tag_configure("done", foreground="#27ae60")
        self._tree.tag_configure("error", foreground="#e74c3c")
        self._tree.tag_configure("pending", foreground="#555")

        vsb = ttk.Scrollbar(list_frame, orient="vertical", command=self._tree.yview)
        self._tree.configure(yscrollcommand=vsb.set)
        self._tree.pack(side="left", fill="both", expand=True)
        vsb.pack(side="right", fill="y")

        self._tree.bind("<<TreeviewSelect>>", self._on_select)
        self._tree.bind("<Delete>", self._remove_selected)

        # Drag-and-drop
        self._tree.drop_target_register(DND_FILES)
        self._tree.dnd_bind("<<Drop>>", self._on_drop)

        # Progress bar
        prog_frame = tk.Frame(left, bg="#f5f5f5")
        prog_frame.pack(fill="x", pady=(4, 0))
        self._prog_var = tk.DoubleVar()
        self._prog_bar = ttk.Progressbar(prog_frame, variable=self._prog_var,
                                          maximum=100)
        self._prog_bar.pack(fill="x", side="left", expand=True)
        self._prog_label = tk.Label(prog_frame, text="", bg="#f5f5f5",
                                     font=("微软雅黑", 9), width=8)
        self._prog_label.pack(side="left", padx=6)

        self._summary_var = tk.StringVar(value="就绪")
        tk.Label(left, textvariable=self._summary_var, bg="#f5f5f5",
                 font=("微软雅黑", 9), fg="#555").pack(anchor="w", pady=(2, 0))

        # Right: options + preview
        right = tk.Frame(top, bg="#f5f5f5")
        top.add(right, minsize=300)

        self._build_options(right)

        # Preview
        preview_frame = ttk.LabelFrame(right, text="预览", padding=6)
        preview_frame.pack(fill="both", expand=True, pady=(6, 0))

        self._preview_label = tk.Label(preview_frame, text="选择文件后显示预览",
                                        bg="#ddd", anchor="center")
        self._preview_label.pack(fill="both", expand=True)

        self._preview_info = tk.Label(preview_frame, text="", font=("微软雅黑", 8), fg="#555")
        self._preview_info.pack()

        # ── Log panel (hidden by default) ──
        self._log_frame = tk.Frame(self._main_pane, bg="#1e1e1e")
        # not added to pane yet — toggled on demand

        log_header = tk.Frame(self._log_frame, bg="#333")
        log_header.pack(fill="x")
        tk.Label(log_header, text="输出日志", bg="#333", fg="white",
                 font=("微软雅黑", 9, "bold"), padx=8).pack(side="left")
        tk.Button(log_header, text="清空", command=self._clear_log,
                  bg="#555", fg="white", relief="flat", font=("微软雅黑", 8),
                  padx=6).pack(side="right", padx=4, pady=2)

        log_text_frame = tk.Frame(self._log_frame)
        log_text_frame.pack(fill="both", expand=True)

        self._log_text = tk.Text(
            log_text_frame, bg="#1e1e1e", fg="#d4d4d4",
            font=("Consolas", 9), wrap="word", state="disabled",
            selectbackground="#264f78"
        )
        log_vsb = ttk.Scrollbar(log_text_frame, orient="vertical",
                                  command=self._log_text.yview)
        self._log_text.configure(yscrollcommand=log_vsb.set)
        self._log_text.pack(side="left", fill="both", expand=True)
        log_vsb.pack(side="right", fill="y")

        # Tag colors for log
        self._log_text.tag_configure("error", foreground="#f48771")
        self._log_text.tag_configure("success", foreground="#4ec9b0")
        self._log_text.tag_configure("info", foreground="#9cdcfe")

    def _build_options(self, parent):
        opt_frame = ttk.LabelFrame(parent, text="压缩参数", padding=10)
        opt_frame.pack(fill="x")

        row = 0

        # Output dir
        tk.Label(opt_frame, text="输出目录：").grid(row=row, column=0, sticky="w", pady=3)
        self._outdir_var = tk.StringVar(value="")
        tk.Entry(opt_frame, textvariable=self._outdir_var, width=20).grid(
            row=row, column=1, sticky="ew", padx=(4, 0))
        tk.Button(opt_frame, text="…", command=self._choose_outdir, width=3).grid(
            row=row, column=2, padx=2)
        row += 1

        # Filename suffix
        tk.Label(opt_frame, text="文件名后缀：").grid(row=row, column=0, sticky="w", pady=3)
        suffix_f = tk.Frame(opt_frame)
        suffix_f.grid(row=row, column=1, columnspan=2, sticky="ew", padx=(4, 0))
        self._suffix_enable_var = tk.BooleanVar(value=False)
        tk.Checkbutton(suffix_f, text="启用", variable=self._suffix_enable_var,
                       command=self._on_suffix_toggle).pack(side="left")
        self._suffix_var = tk.StringVar(value="_compressed")
        self._suffix_entry = tk.Entry(suffix_f, textvariable=self._suffix_var,
                                       width=14, state="disabled")
        self._suffix_entry.pack(side="left", padx=(4, 0))
        row += 1

        # Output format
        tk.Label(opt_frame, text="输出格式：").grid(row=row, column=0, sticky="w", pady=3)
        self._fmt_var = tk.StringVar(value="same")
        fmt_combo = ttk.Combobox(opt_frame, textvariable=self._fmt_var, width=10,
                                  values=["same", "JPEG", "PNG", "WEBP", "TGA"], state="readonly")
        fmt_combo.grid(row=row, column=1, sticky="w", padx=(4, 0))
        tk.Label(opt_frame, text="same=原格式", fg="#888", font=("", 8)).grid(
            row=row, column=2, sticky="w")
        row += 1

        # Quality
        tk.Label(opt_frame, text="质量 (JPEG/WEBP)：").grid(row=row, column=0, sticky="w", pady=3)
        q_frame = tk.Frame(opt_frame)
        q_frame.grid(row=row, column=1, columnspan=2, sticky="ew")
        self._quality_var = tk.IntVar(value=80)
        tk.Scale(q_frame, from_=1, to=100, orient="horizontal",
                 variable=self._quality_var, length=130,
                 command=lambda v: self._quality_lbl.config(text=str(int(float(v))))).pack(side="left")
        self._quality_lbl = tk.Label(q_frame, text="80", width=3)
        self._quality_lbl.pack(side="left")
        row += 1

        # Max dimensions
        tk.Label(opt_frame, text="最大宽度 (px)：").grid(row=row, column=0, sticky="w", pady=3)
        dim_f = tk.Frame(opt_frame)
        dim_f.grid(row=row, column=1, columnspan=2, sticky="w", padx=(4, 0))
        self._maxw_var = tk.StringVar(value="0")
        tk.Entry(dim_f, textvariable=self._maxw_var, width=7).pack(side="left")
        tk.Label(dim_f, text="  高：", fg="#555").pack(side="left")
        self._maxh_var = tk.StringVar(value="0")
        tk.Entry(dim_f, textvariable=self._maxh_var, width=7).pack(side="left")
        tk.Label(dim_f, text="0=不限", fg="#888", font=("", 8)).pack(side="left", padx=4)
        row += 1

        # PNG options
        png_frame = ttk.LabelFrame(opt_frame, text="PNG 专属", padding=6)
        png_frame.grid(row=row, column=0, columnspan=3, sticky="ew", pady=(6, 0))

        self._png_lossless_var = tk.BooleanVar(value=False)
        tk.Checkbutton(png_frame, text="无损压缩 (optimize + 最高压缩级别)",
                       variable=self._png_lossless_var).pack(anchor="w")

        self._png_quantize_var = tk.BooleanVar(value=False)
        tk.Checkbutton(png_frame, text="有损量化 (减少颜色数量，体积更小)",
                       variable=self._png_quantize_var).pack(anchor="w")

        colors_f = tk.Frame(png_frame)
        colors_f.pack(anchor="w")
        tk.Label(colors_f, text="  颜色数量：").pack(side="left")
        self._png_colors_var = tk.IntVar(value=256)
        tk.Spinbox(colors_f, from_=2, to=256, textvariable=self._png_colors_var,
                   width=5).pack(side="left")
        tk.Label(colors_f, text="(2–256)", fg="#888", font=("", 8)).pack(side="left", padx=4)
        row += 1

        # Other
        other_f = tk.Frame(opt_frame)
        other_f.grid(row=row, column=0, columnspan=3, sticky="w", pady=(4, 0))
        self._keep_exif_var = tk.BooleanVar(value=False)
        tk.Checkbutton(other_f, text="保留 EXIF 信息",
                       variable=self._keep_exif_var).pack(anchor="w")

        opt_frame.columnconfigure(1, weight=1)

    # ──────────────────────────── LOG PANEL ────────────────────────────

    def _toggle_log(self):
        if self._show_log.get():
            self._show_log.set(False)
            self._main_pane.forget(self._log_frame)
            self._log_btn.config(text="显示日志 ▼")
        else:
            self._show_log.set(True)
            self._main_pane.add(self._log_frame, minsize=120)
            self._log_btn.config(text="隐藏日志 ▲")

    def _log(self, text: str, tag: str = "info"):
        self._log_text.config(state="normal")
        self._log_text.insert("end", text + "\n", tag)
        self._log_text.see("end")
        self._log_text.config(state="disabled")

    def _clear_log(self):
        self._log_text.config(state="normal")
        self._log_text.delete("1.0", "end")
        self._log_text.config(state="disabled")

    # ──────────────────────────── FILE OPS ────────────────────────────

    def _on_suffix_toggle(self):
        if self._suffix_enable_var.get():
            self._suffix_entry.config(state="normal")
        else:
            self._suffix_entry.config(state="disabled")

    def _on_drop(self, event):
        # tkinterdnd2 returns paths as a Tcl list: {path with spaces} path2 ...
        raw = self.tk.splitlist(event.data)
        paths = []
        for entry in raw:
            p = Path(entry)
            if p.is_dir():
                paths += [str(f) for f in p.rglob("*")
                          if f.suffix.lower() in SUPPORTED_FORMATS]
            elif p.is_file() and p.suffix.lower() in SUPPORTED_FORMATS:
                paths.append(str(p))
        self._add_paths(paths)

    def _add_files(self):
        paths = filedialog.askopenfilenames(
            title="选择图片",
            filetypes=[
                ("图片文件", "*.jpg *.jpeg *.png *.webp *.bmp *.tiff *.tif *.gif *.tga"),
                ("所有文件", "*.*"),
            ]
        )
        self._add_paths(paths)

    def _add_folder(self):
        folder = filedialog.askdirectory(title="选择文件夹")
        if not folder:
            return
        paths = [str(f) for f in Path(folder).rglob("*")
                 if f.suffix.lower() in SUPPORTED_FORMATS]
        self._add_paths(paths)

    def _add_paths(self, paths):
        existing = set(self._input_files)
        added = 0
        for p in paths:
            if p not in existing:
                self._input_files.append(p)
                existing.add(p)
                size = os.path.getsize(p)
                self._tree.insert("", "end", iid=p, values=(
                    Path(p).name, get_file_size_str(size), "待压缩", "-"
                ), tags=("pending",))
                added += 1
        self._summary_var.set(f"共 {len(self._input_files)} 个文件，新增 {added} 个")

    def _clear_list(self):
        self._input_files.clear()
        self._log_data.clear()
        for item in self._tree.get_children():
            self._tree.delete(item)
        self._summary_var.set("就绪")
        self._prog_var.set(0)
        self._prog_label.config(text="")

    def _remove_selected(self, event=None):
        for iid in self._tree.selection():
            self._tree.delete(iid)
            if iid in self._input_files:
                self._input_files.remove(iid)
            self._log_data.pop(iid, None)

    def _choose_outdir(self):
        d = filedialog.askdirectory(title="选择输出目录")
        if d:
            self._outdir_var.set(d)

    # ──────────────────────────── PREVIEW ────────────────────────────

    def _on_select(self, event=None):
        sel = self._tree.selection()
        if not sel:
            return
        path = sel[0]
        # Show per-file log when log panel is open
        if self._show_log.get() and path in self._log_data:
            self._clear_log()
            tag = "success" if self._log_data[path]["success"] else "error"
            self._log(self._log_data[path]["log"], tag)

        if self._preview_job:
            self.after_cancel(self._preview_job)
        self._preview_job = self.after(80, lambda: self._load_preview(path))

    def _load_preview(self, path):
        try:
            img = Image.open(path)
            w, h = img.size
            fmt = img.format or "?"
            mode = img.mode
            size_str = get_file_size_str(os.path.getsize(path))

            pw = max(self._preview_label.winfo_width(), 240)
            ph = max(self._preview_label.winfo_height(), 160)

            # preview needs RGB/RGBA
            if img.mode not in ("RGB", "RGBA", "L", "LA"):
                img = img.convert("RGB")
            img.thumbnail((pw, ph), Image.LANCZOS)
            photo = ImageTk.PhotoImage(img)
            self._preview_label.config(image=photo, text="")
            self._preview_label._photo = photo
            self._preview_info.config(text=f"{w}×{h}  {fmt}  {mode}  {size_str}")
        except Exception as e:
            self._preview_label.config(image="", text=f"预览失败\n{e}")
            self._preview_info.config(text="")

    # ──────────────────────────── COMPRESS ────────────────────────────

    def _get_options(self):
        try:
            maxw = int(self._maxw_var.get())
        except ValueError:
            maxw = 0
        try:
            maxh = int(self._maxh_var.get())
        except ValueError:
            maxh = 0

        suffix_tag = ""
        if self._suffix_enable_var.get():
            suffix_tag = self._suffix_var.get().strip()

        return {
            "quality": self._quality_var.get(),
            "max_width": maxw,
            "max_height": maxh,
            "keep_exif": self._keep_exif_var.get(),
            "lossless": False,
            "png_lossless": self._png_lossless_var.get(),
            "output_format": self._fmt_var.get(),
            "png_quantize": self._png_quantize_var.get(),
            "png_colors": self._png_colors_var.get(),
            "suffix_tag": suffix_tag,
        }

    def _start(self):
        if not self._input_files:
            messagebox.showwarning("提示", "请先添加图片文件")
            return

        outdir = self._outdir_var.get().strip()
        if not outdir:
            outdir = str(Path(self._input_files[0]).parent / "compressed")
            self._outdir_var.set(outdir)

        options = self._get_options()

        # Reset states
        self._log_data.clear()
        for iid in self._tree.get_children():
            self._tree.item(iid, values=(
                Path(iid).name,
                get_file_size_str(os.path.getsize(iid)),
                "待压缩", "-"
            ), tags=("pending",))

        if self._show_log.get():
            self._clear_log()
            self._log(f"开始压缩，共 {len(self._input_files)} 个文件  →  {outdir}", "info")

        self._cancel_flag[0] = False
        self._btn_start.config(state="disabled")
        self._btn_cancel.config(state="normal")
        self._prog_var.set(0)
        self._summary_var.set("压缩中…")

        threading.Thread(
            target=self._compress_thread,
            args=(list(self._input_files), outdir, options),
            daemon=True,
        ).start()

    def _cancel(self):
        self._cancel_flag[0] = True
        self._btn_cancel.config(state="disabled")
        self._summary_var.set("正在取消…")

    def _compress_thread(self, files, outdir, options):
        total_in = total_out = 0

        def on_progress(current, total_, res):
            nonlocal total_in, total_out
            pct = current / total_ * 100
            self.after(0, self._update_progress, pct, current, total_, res)
            if res["success"]:
                total_in += res["input_size"]
                total_out += res["output_size"]

        batch_compress(
            input_paths=files,
            output_dir=outdir,
            options=options,
            progress_callback=on_progress,
            cancel_flag=self._cancel_flag,
        )
        self.after(0, self._compress_done, total_in, total_out)

    def _update_progress(self, pct, current, total, res):
        self._prog_var.set(pct)
        self._prog_label.config(text=f"{current}/{total}")

        iid = res["input_path"]
        self._log_data[iid] = res

        if not self._tree.exists(iid):
            return

        if res["success"]:
            ratio_str = f"{res['ratio']:.1f}%"
            out_size = get_file_size_str(res["output_size"])
            self._tree.item(iid, values=(
                Path(iid).name,
                get_file_size_str(res["input_size"]),
                f"✓ {out_size}",
                ratio_str,
            ), tags=("done",))
        else:
            self._tree.item(iid, values=(
                Path(iid).name,
                get_file_size_str(res["input_size"]),
                "✗ 失败",
                "-",
            ), tags=("error",))

        # Append to log panel if visible
        if self._show_log.get():
            tag = "success" if res["success"] else "error"
            self._log(f"{'✓' if res['success'] else '✗'} {Path(iid).name}", tag)
            if not res["success"]:
                self._log(f"  错误：{res['error']}", "error")

    def _compress_done(self, total_in, total_out):
        self._btn_start.config(state="normal")
        self._btn_cancel.config(state="disabled")
        self._prog_var.set(100)

        if total_in > 0:
            saved = total_in - total_out
            ratio = saved / total_in * 100
            msg = (
                f"完成！{get_file_size_str(total_in)} → "
                f"{get_file_size_str(total_out)}  "
                f"节省 {get_file_size_str(saved)} ({ratio:.1f}%)"
            )
        else:
            msg = "完成！（所有文件均失败，请打开日志查看详情）"

        self._summary_var.set(msg)

        if self._show_log.get():
            self._log(f"\n{msg}", "success" if total_in > 0 else "error")

        outdir = self._outdir_var.get()
        if total_in > 0 and messagebox.askyesno("完成", f"{msg}\n\n是否打开输出目录？"):
            os.startfile(outdir)
        elif total_in == 0:
            messagebox.showerror("全部失败", "所有文件压缩均失败。\n请点击「显示日志」按钮查看具体错误信息。")


if __name__ == "__main__":
    app = App()
    app.mainloop()
