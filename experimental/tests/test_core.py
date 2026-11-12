import json
import tempfile
import unittest
from pathlib import Path
from yochat_portable.core import Transcript, Settings, WakeGate, placement, parse_hotkey


def snapshot(*rows):
    return json.dumps({'version': 1, 'threads': {'test': {'items': [dict(role=r, text=t) for r, t in rows]}}})


class CoreTests(unittest.TestCase):
    def test_baseline_and_duplicate_updates(self):
        r = Transcript()
        r.update(snapshot(('user', 'Old')))
        self.assertEqual(r.rows, [])
        r.update(snapshot(('user', 'Hi'), ('user', 'Hi'), ('assistant', 'Hello'), ('assistant', 'Hello')))
        self.assertEqual(r.rows, [('user', 'Hi'), ('assistant', 'Hello')])
        r.update(snapshot(('user', 'Hi'), ('assistant', 'Hello')))
        self.assertEqual(len(r.rows), 2)

    def test_repeats_prefixes_and_window(self):
        r = Transcript(); r.update(snapshot())
        r.update(snapshot(('user', 'Yes'), ('assistant', 'Yes'), ('user', 'Yes'), ('user', 'Yes please'), ('assistant', 'Sure'), ('assistant', 'Sure')))
        self.assertEqual(len(r.rows), 5)
        self.assertEqual(r.rows[0], ('user', 'Yes'))

    def test_partial_write_preserves_state(self):
        r = Transcript(); r.update(snapshot()); r.update(snapshot(('user', 'New')))
        with self.assertRaises(ValueError): r.update('{')
        self.assertEqual(r.rows, [('user', 'New')])

    def test_reset_and_new_call(self):
        r = Transcript(); r.update(snapshot()); r.update(snapshot(('user', 'First')))
        r.reset(); r.update(snapshot(('user', 'First')))
        self.assertEqual(r.rows, [])
        r.update(json.dumps({'version': 1, 'threads': {'second': {'items': [{'role': 'user', 'text': 'Second'}]}}}))
        self.assertEqual(r.rows, [('user', 'Second')])

    def test_bad_schema_is_not_published(self):
        r = Transcript(); r.update(snapshot())
        for bad in [{'version': 2, 'threads': {}}, {'version': 1, 'threads': []}, {'version': 1, 'threads': {'x': {'items': [{'role': 'user', 'text': 4}]}}}]:
            with self.assertRaises(ValueError): r.update(json.dumps(bad))

    def test_wake_blocks_unknown_stale_active_and_pending(self):
        g = WakeGate()
        self.assertFalse(g.accept('yo chat', 1, 'yo chat', .78, 4, 10, None, 10))
        self.assertFalse(g.accept('yo chat', 1, 'yo chat', .78, 4, 10, False, 8))
        self.assertFalse(g.accept('yo chat', 1, 'yo chat', .78, 4, 10, True, 10))
        self.assertTrue(g.accept('Yo Chat', .9, 'yo chat', .78, 4, 10, False, 10))
        self.assertFalse(g.accept('yo chat', 1, 'yo chat', .78, 4, 15, False, 15))
        self.assertTrue(g.accept('yo chat', 1, 'yo chat', .78, 4, 21, False, 21))

    def test_wake_confidence_and_exact_phrase(self):
        g = WakeGate()
        self.assertFalse(g.accept('yo chat please', 1, 'yo chat', .78, 4, 10, False, 10))
        self.assertFalse(g.accept('yo chat', .4, 'yo chat', .78, 4, 10, False, 10))

    def test_settings_round_trip_and_validation(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'settings.json'
            s = Settings(); s.phrase = 'hello computer'; s.save(path)
            self.assertEqual(Settings.load(path).phrase, s.phrase)
            s.confidence = 2
            with self.assertRaises(ValueError): s.save(path)
            self.assertEqual(Settings.load(path).confidence, .78)

    def test_hotkey_no_arbitrary_commands(self):
        self.assertEqual(parse_hotkey('Ctrl+Alt+Space'), ['ctrl', 'alt', 'space'])
        self.assertEqual(parse_hotkey('Command+Shift+V'), ['cmd', 'shift', 'v'])
        for value in ['echo hi', 'ctrl', 'a+b', 'ctrl+ctrl+a', 'ctrl+;']:
            with self.assertRaises(ValueError): parse_hotkey(value)

    def test_two_axis_geometry_and_edges(self):
        self.assertEqual(placement(400, 500, 320, 200, 28, (0, 0, 1920, 1080)), (240, 272))
        self.assertEqual(placement(-400, 300, 320, 200, 28, (-1920, 0, 1920, 1080)), (-560, 72))
        self.assertIsNone(placement(400, 100, 320, 200, 28, (0, 0, 1920, 1080)))

if __name__ == '__main__': unittest.main()
