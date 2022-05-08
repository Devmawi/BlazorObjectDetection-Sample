﻿using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorObjectDetectionApp.DataStructures
{
    public class ImageNetPrediction
    {

        [ColumnName("grid")]
        public float[] PredictedLabels;
    }
}
