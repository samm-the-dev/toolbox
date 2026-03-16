# FFmpeg Reference

Common patterns and gotchas for video/audio processing with FFmpeg. For core rules, see [AGENTS.md](AGENTS.md).

## Audio

### EAC3 5.1 → Stereo AAC

EAC3 5.1 (common in Netflix/streaming rips) needs `-ac 2` to properly downmix to stereo.
Without it, ffmpeg preserves the 5.1 channel layout, causing a weird L/R split in stereo playback:

```bash
ffmpeg -i input.mkv -c:v copy -c:a aac -b:a 192k -ac 2 output.mp4
```

## Video

### Letterbox Crop Detection

Use `cropdetect` to find the crop parameters before committing:

```bash
ffmpeg -i input.mkv -vf cropdetect -f null - 2>&1 | grep crop=
```

Take the most common `crop=W:H:X:Y` value from the output, then apply it:

```bash
ffmpeg -i input.mkv -vf crop=W:H:X:Y output.mkv
```

### Stream-Copy Cutting

Cut a clip without re-encoding (fast, lossless):

```bash
ffmpeg -ss 00:19:00 -to 00:22:30 -i input.mkv -c copy output.mkv
```

Put `-ss` before `-i` for fast keyframe seek. Note: start/end may be slightly imprecise (keyframe-aligned); re-encode if frame-accurate cuts are needed.

**Preview before cutting:** VLC accepts `--start-time` and `--stop-time` (in seconds) to play a range without writing a file:

```bash
vlc "input.mkv" --start-time=1140 --stop-time=1230
```

### Probe Duration and Stream Info

```bash
ffprobe -v quiet -show_entries format=duration -of default=noprint_wrappers=1 input.mp4
ffprobe -v quiet -show_entries stream=width,height,codec_name -of default=noprint_wrappers=1 input.mp4
```
