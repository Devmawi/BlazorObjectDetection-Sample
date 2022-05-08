# Blazor Object Detection 

Simple project for demonstrating how to embed a continuously object detection with Yolo 
on a video in a hybrid Blazor app (WebView2).

## Main components and technologies:
	
* Hybrid Blazor with [BlazorWebView](https://medium.com/@devmawin/software-development-and-hybrid-app-development-with-blazorwebview-blazor-59297f399811)
* VideoCapture service based on [VLC Media Player](https://github.com/videolan/libvlcsharp#samples)
* [ML.NET and ONNX runtime for object detection](https://docs.microsoft.com/en-us/dotnet/machine-learning/tutorials/object-detection-onnx)
* [Tiny YOLOv2 form the ONNX model zoo](https://github.com/onnx/models/tree/main/vision/object_detection_segmentation/tiny-yolov2)

