"""Qt frontend. Native access and speech never run in the GUI thread."""
import argparse
import json
import math
from pathlib import Path
import sys
import time

from PySide6.QtCore import QLockFile, QPoint, QRectF, Qt, QTimer
from PySide6.QtGui import QColor, QFont, QFontMetrics, QIcon, QPainter, QPixmap
from PySide6.QtWidgets import (QApplication, QCheckBox, QDialog, QDialogButtonBox,
    QDoubleSpinBox, QFormLayout, QLabel, QLineEdit, QMenu, QMessageBox,
    QSpinBox, QSystemTrayIcon, QWidget)

from .core import Settings, data_dir, placement
from .engine import Engine


class Bubbles(QWidget):
    def __init__(self):
        super().__init__(None, Qt.WindowType.Tool | Qt.WindowType.FramelessWindowHint |
                         Qt.WindowType.WindowStaysOnTopHint | Qt.WindowType.WindowDoesNotAcceptFocus)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        self.setAttribute(Qt.WidgetAttribute.WA_ShowWithoutActivating)
        self.setWindowTitle('YoChat Experimental Bubbles')
        self.setFont(QFont('Helvetica Neue' if sys.platform == 'darwin' else 'Noto Sans', 11))
        self.drawn = []
        self.last_rows = None
        self.last_budget = None

    def render_rows(self, rows, budget):
        if rows == self.last_rows and budget == self.last_budget:
            return
        self.last_rows = list(rows); self.last_budget = budget
        metrics = QFontMetrics(self.font())
        entries = []
        for role, text in rows[-5:]:
            bounds = metrics.boundingRect(0, 0, 250, 10000, int(Qt.TextFlag.TextWordWrap), text)
            h = bounds.height() + 18
            full = h > max(48, budget - 16)
            shown = 'Read full message…' if full else text
            if full:
                bounds = metrics.boundingRect(shown); h = metrics.height() + 18
            width = min(278, max(50, bounds.width() + 28))
            entries.append((role, text, shown, width, h, full))
        while entries and sum(entry[4] + 6 for entry in entries) + 10 > budget:
            entries.pop(0)
        y = 4; self.drawn = []
        for role, text, shown, width, h, full in entries:
            x = 310 - width if role == 'user' else 10
            self.drawn.append((QRectF(x, y, width, h), role, text, shown, full))
            y += h + 6
        self.resize(320, max(1, int(y + 4)))
        self.update()

    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        painter.setPen(Qt.PenStyle.NoPen)
        for rect, role, text, shown, full in self.drawn:
            painter.setBrush(QColor('#4A79EB' if role == 'user' else '#303035'))
            painter.setPen(Qt.PenStyle.NoPen)
            painter.drawRoundedRect(rect, 19, 19)
            painter.setPen(QColor('white'))
            painter.drawText(rect.adjusted(14, 8, -14, -8), Qt.TextFlag.TextWordWrap | Qt.AlignmentFlag.AlignVCenter, shown)

    def mousePressEvent(self, event):
        for rect, role, text, shown, full in self.drawn:
            if full and rect.contains(event.position()):
                dialog = QMessageBox(self)
                dialog.setWindowTitle('Full voice message')
                dialog.setTextFormat(Qt.TextFormat.PlainText)
                dialog.setText(text); dialog.exec()
                return


class SettingsDialog(QDialog):
    def __init__(self, config):
        super().__init__()
        self.setWindowTitle('YoChat Settings — Experimental')
        self.setMinimumWidth(480)
        layout = QFormLayout(self)
        note = QLabel('Match the hotkey configured in ChatGPT.\nChanges apply after Save; microphone access may be requested by your OS.')
        note.setWordWrap(True); layout.addRow(note)
        self.phrase = QLineEdit(config.phrase); layout.addRow('Wake phrase', self.phrase)
        self.hotkey = QLineEdit(config.hotkey); layout.addRow('Voice hotkey', self.hotkey)
        self.confidence = QDoubleSpinBox(); self.confidence.setRange(.5, 1); self.confidence.setSingleStep(.01)
        self.confidence.setValue(config.confidence); layout.addRow('Confidence', self.confidence)
        self.cooldown = QDoubleSpinBox(); self.cooldown.setRange(1, 60); self.cooldown.setValue(config.cooldown)
        layout.addRow('Cooldown (seconds)', self.cooldown)
        self.gap = QSpinBox(); self.gap.setRange(0, 300); self.gap.setValue(config.gap); layout.addRow('Space above pill', self.gap)
        self.bubbles = QCheckBox('Show live bubbles'); self.bubbles.setChecked(config.bubbles); layout.addRow(self.bubbles)
        self.transcript = QLineEdit(config.transcript); self.transcript.setPlaceholderText('Automatic')
        layout.addRow('Transcript file', self.transcript)
        self.model = QLineEdit(config.model); self.model.setPlaceholderText('Bundled English model')
        layout.addRow('Vosk model folder', self.model)
        self.error = QLabel(); self.error.setWordWrap(True); layout.addRow(self.error)
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Save | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self.check); buttons.rejected.connect(self.reject); layout.addRow(buttons)
        self.result_settings = None

    def check(self):
        try:
            self.result_settings = Settings(self.phrase.text(), self.hotkey.text(), self.confidence.value(),
                self.cooldown.value(), self.gap.value(), self.bubbles.isChecked(), self.transcript.text(), self.model.text()).validate()
            self.accept()
        except ValueError as error:
            self.error.setText(str(error))


class Controller:
    def __init__(self, app, config, path, preview=False):
        self.app = app; self.path = path; self.config = config; self.preview = preview
        self.engine = None if preview else Engine(config)
        self.view = Bubbles()
        pixmap = QPixmap(32, 32); pixmap.fill(Qt.GlobalColor.transparent)
        painter = QPainter(pixmap); painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        painter.setBrush(QColor('#4A79EB')); painter.setPen(Qt.PenStyle.NoPen); painter.drawEllipse(1, 1, 30, 30)
        painter.setPen(QColor('white')); painter.drawText(pixmap.rect(), Qt.AlignmentFlag.AlignCenter, 'Y'); painter.end()
        self.tray = QSystemTrayIcon(QIcon(pixmap), app)
        self.tray.setToolTip('YoChat — Experimental')
        menu = QMenu()
        menu.addAction('YoChat — Experimental').setEnabled(False)
        menu.addAction('Settings…', self.settings)
        menu.addAction('Connection status…', self.status)
        pause = menu.addAction('Pause listening'); pause.setCheckable(True); pause.toggled.connect(self.pause)
        menu.addSeparator(); menu.addAction('Exit YoChat', app.quit)
        self.menu = menu; self.tray.setContextMenu(menu); self.tray.show()
        if self.engine:
            self.engine.start()
        self.timer = QTimer(); self.timer.timeout.connect(self.tick); self.timer.start(16)
        app.aboutToQuit.connect(self.close)

    def pause(self, value):
        if self.engine:
            with self.engine.lock:
                self.engine.paused = value

    def settings(self):
        dialog = SettingsDialog(self.config)
        if dialog.exec() == QDialog.DialogCode.Accepted:
            try:
                dialog.result_settings.save(self.path)
                self.config = dialog.result_settings
                if self.engine:
                    self.engine.configure(self.config)
            except (OSError, ValueError) as error:
                QMessageBox.warning(None, 'Cannot save settings', str(error))

    def status(self):
        state = self.engine.state() if self.engine else dict(native='Preview only', speech='Microphone off', transcript='Synthetic messages')
        dialog = QMessageBox()
        dialog.setWindowTitle('YoChat connection — Experimental')
        dialog.setTextFormat(Qt.TextFormat.PlainText)
        dialog.setText('\n\n'.join(state[k] for k in ('native', 'speech', 'transcript')))
        dialog.exec()

    def tick(self):
        if self.preview:
            screen = self.app.primaryScreen().availableGeometry()
            state = dict(pill={'x': screen.center().x(), 'top': screen.bottom() - 100}, observed_at=time.monotonic(),
                         rows=[('user', 'Can you help me plan my afternoon?'), ('assistant', 'Of course. What would you like to get done?')])
        else:
            state = self.engine.state()
        pill = state['pill']
        if not self.config.bubbles or not pill or not state['rows'] or time.monotonic() - state['observed_at'] > 1.5:
            self.view.hide(); return
        if not all(isinstance(pill.get(k), (int, float)) and math.isfinite(pill[k]) for k in ('x', 'top')):
            self.view.hide(); return
        screen = self.app.screenAt(QPoint(int(pill['x']), int(pill['top'])))
        if not screen:
            self.view.hide(); return
        area = screen.availableGeometry()
        budget = int(min(480, pill['top'] - self.config.gap - area.top()))
        self.view.render_rows(state['rows'], budget)
        target = placement(pill['x'], pill['top'], self.view.width(), self.view.height(), self.config.gap,
                           (area.x(), area.y(), area.width(), area.height()))
        if not target or not self.view.drawn:
            self.view.hide(); return
        # Coordinates from macOS AX are points; Linux X11 is run with Qt scale 1.
        self.view.move(*target)
        self.view.show()

    def close(self):
        self.timer.stop(); self.view.close(); self.tray.hide()
        if self.engine:
            self.engine.close()


def main(argv=None):
    parser = argparse.ArgumentParser(description='YoChat macOS/Linux — experimental')
    parser.add_argument('--preview', action='store_true', help='Synthetic bubbles, no microphone or ChatGPT access')
    parser.add_argument('--smoke-test', action='store_true', help='Render widgets briefly, then exit without native access')
    args = parser.parse_args(argv)
    if not args.preview and not args.smoke_test and sys.platform not in ('darwin', 'linux'):
        parser.error('Use the existing Windows application on Windows.')
    app = QApplication(sys.argv[:1]); app.setQuitOnLastWindowClosed(False)
    app.setApplicationName('YoChat Experimental')
    preview = args.preview or args.smoke_test
    path = data_dir() / 'settings.json'
    lease = None
    if not preview:
        path.parent.mkdir(parents=True, exist_ok=True)
        lease = QLockFile(str(path.parent / 'instance.lock')); lease.setStaleLockTime(0)
        if not lease.tryLock(0):
            print('YoChat Experimental is already running.'); return 0
    try:
        config = Settings() if preview else Settings.load(path)
    except (OSError, ValueError) as error:
        QMessageBox.critical(None, 'YoChat settings error', str(error)); return 1
    controller = Controller(app, config, path, preview)
    if args.smoke_test:
        dialog = SettingsDialog(config); dialog.show(); dialog.close()
        controller.tick()
        if not controller.view.drawn:
            raise RuntimeError('Bubble layout did not render.')
        QTimer.singleShot(250, app.quit)
    elif not QSystemTrayIcon.isSystemTrayAvailable():
        QMessageBox.warning(None, 'Tray unavailable', 'Enable a system tray/AppIndicator extension for YoChat controls.')
        controller.close(); return 1
    return app.exec()


if __name__ == '__main__':
    raise SystemExit(main())
