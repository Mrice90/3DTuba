import sys, json, mediapipe as mp
from mediapipe.tasks.python import vision, BaseOptions
det=vision.PoseLandmarker.create_from_options(vision.PoseLandmarkerOptions(base_options=BaseOptions(model_asset_path=sys.argv[1]),min_pose_detection_confidence=0.2,min_pose_presence_confidence=0.2))
for p in sys.argv[2:]:
    r=det.detect(mp.Image.create_from_file(p))
    if not r.pose_landmarks: print(p,"NONE"); continue
    lm=r.pose_landmarks[0]
    print(p, len(lm), [(i,round(l.x,3),round(l.y,3),round(l.visibility,2)) for i,l in enumerate(lm) if i in (0,11,12,13,14,15,16,23,24,25,26,27,28,31,32)])
