﻿using System;

namespace MingStar.SimUniversity.AI.Learning
{
    // taken from http://nelder-mead-simplex.googlecode.com/
    public delegate double ObjectiveFunctionDelegate(double[] constants);

    public sealed class NelderMeadSimplex
    {
        private const double JITTER = 1e-10d; // a small value used to protect against floating point noise

        public static RegressionResult Regress(SimplexConstant[] simplexConstants, double convergenceTolerance,
                                               int maxEvaluations,
                                               ObjectiveFunctionDelegate objectiveFunction)
        {
            // confirm that we are in a position to commence
            if (objectiveFunction == null)
                throw new InvalidOperationException("ObjectiveFunction must be set to a valid ObjectiveFunctionDelegate");

            if (simplexConstants == null)
                throw new InvalidOperationException("SimplexConstants must be initialized");

            // create the initial simplex
            int numDimensions = simplexConstants.Length;
            int numVertices = numDimensions + 1;
            Vector[] vertices = _initializeVertices(simplexConstants);

            int evaluationCount = 0;
            TerminationReason terminationReason;
            ErrorProfile errorProfile;

            double[] errorValues = _initializeErrorValues(vertices, objectiveFunction);

            // iterate until we converge, or complete our permitted number of iterations
            while (true)
            {
                errorProfile = _evaluateSimplex(errorValues);

                // see if the range in point heights is small enough to exit
                if (_hasConverged(convergenceTolerance, errorProfile, errorValues))
                {
                    terminationReason = TerminationReason.Converged;
                    break;
                }

                // attempt a reflection of the simplex
                double reflectionPointValue = _tryToScaleSimplex(-1.0, ref errorProfile, vertices, errorValues,
                                                                 objectiveFunction);
                ++evaluationCount;
                if (reflectionPointValue <= errorValues[errorProfile.LowestIndex])
                {
                    // it's better than the best point, so attempt an expansion of the simplex
                    double expansionPointValue = _tryToScaleSimplex(2.0, ref errorProfile, vertices, errorValues,
                                                                    objectiveFunction);
                    ++evaluationCount;
                }
                else if (reflectionPointValue >= errorValues[errorProfile.NextHighestIndex])
                {
                    // it would be worse than the second best point, so attempt a contraction to look
                    // for an intermediate point
                    double currentWorst = errorValues[errorProfile.HighestIndex];
                    double contractionPointValue = _tryToScaleSimplex(0.5, ref errorProfile, vertices, errorValues,
                                                                      objectiveFunction);
                    ++evaluationCount;
                    if (contractionPointValue >= currentWorst)
                    {
                        // that would be even worse, so let's try to contract uniformly towards the low point; 
                        // don't bother to update the error profile, we'll do it at the start of the
                        // next iteration
                        _shrinkSimplex(errorProfile, vertices, errorValues, objectiveFunction);
                        evaluationCount += numVertices;
                        // that required one function evaluation for each vertex; keep track
                    }
                }
                // check to see if we have exceeded our alloted number of evaluations
                if (evaluationCount >= maxEvaluations)
                {
                    terminationReason = TerminationReason.MaxFunctionEvaluations;
                    break;
                }
            }
            var regressionResult = new RegressionResult(terminationReason,
                                                        vertices[errorProfile.LowestIndex].Components,
                                                        errorValues[errorProfile.LowestIndex], evaluationCount);
            return regressionResult;
        }

        /// <summary>
        /// Evaluate the objective function at each vertex to create a corresponding
        /// list of error values for each vertex
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="objectiveFunction"></param>
        /// <returns></returns>        
        private static double[] _initializeErrorValues(Vector[] vertices, ObjectiveFunctionDelegate objectiveFunction)
        {
            var errorValues = new double[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                errorValues[i] = objectiveFunction(vertices[i].Components);
            }
            return errorValues;
        }

        /// <summary>
        /// Check whether the points in the error profile have so little range that we
        /// consider ourselves to have converged
        /// </summary>
        /// <param name="convergenceTolerance"></param>
        /// <param name="errorProfile"></param>
        /// <param name="errorValues"></param>
        /// <returns></returns>
        private static bool _hasConverged(double convergenceTolerance, ErrorProfile errorProfile, double[] errorValues)
        {
            double range = 2*Math.Abs(errorValues[errorProfile.HighestIndex] - errorValues[errorProfile.LowestIndex])/
                           (Math.Abs(errorValues[errorProfile.HighestIndex]) +
                            Math.Abs(errorValues[errorProfile.LowestIndex]) + JITTER);
            return range < convergenceTolerance;
        }

        /// <summary>
        /// Examine all error values to determine the ErrorProfile
        /// </summary>
        /// <param name="errorValues"></param>
        /// <returns></returns>
        private static ErrorProfile _evaluateSimplex(double[] errorValues)
        {
            var errorProfile = new ErrorProfile();
            if (errorValues[0] > errorValues[1])
            {
                errorProfile.HighestIndex = 0;
                errorProfile.NextHighestIndex = 1;
            }
            else
            {
                errorProfile.HighestIndex = 1;
                errorProfile.NextHighestIndex = 0;
            }

            for (int index = 0; index < errorValues.Length; index++)
            {
                var errorValue = errorValues[index];
                if (errorValue <= errorValues[errorProfile.LowestIndex])
                {
                    errorProfile.LowestIndex = index;
                }
                if (errorValue > errorValues[errorProfile.HighestIndex])
                {
                    errorProfile.NextHighestIndex = errorProfile.HighestIndex;
                    // downgrade the current highest to next highest
                    errorProfile.HighestIndex = index;
                }
                else if (errorValue > errorValues[errorProfile.NextHighestIndex] && index != errorProfile.HighestIndex)
                {
                    errorProfile.NextHighestIndex = index;
                }
            }

            return errorProfile;
        }

        /// <summary>
        /// Construct an initial simplex, given starting guesses for the constants, and
        /// initial step sizes for each dimension
        /// </summary>
        /// <param name="simplexConstants"></param>
        /// <returns></returns>
        private static Vector[] _initializeVertices(SimplexConstant[] simplexConstants)
        {
            int numDimensions = simplexConstants.Length;
            var vertices = new Vector[numDimensions + 1];

            // define one point of the simplex as the given initial guesses
            var p0 = new Vector(numDimensions);
            for (int i = 0; i < numDimensions; i++)
            {
                p0[i] = simplexConstants[i].Value;
            }

            // now fill in the vertices, creating the additional points as:
            // P(i) = P(0) + Scale(i) * UnitVector(i)
            vertices[0] = p0;
            for (int i = 0; i < numDimensions; i++)
            {
                double scale = simplexConstants[i].InitialPerturbationScale;
                var unitVector = new Vector(numDimensions);
                unitVector[i] = 1;
                vertices[i + 1] = p0.Add(unitVector.Multiply(scale));
            }
            return vertices;
        }

        /// <summary>
        /// Test a scaling operation of the high point, and replace it if it is an improvement
        /// </summary>
        /// <param name="scaleFactor"></param>
        /// <param name="errorProfile"></param>
        /// <param name="vertices"></param>
        /// <param name="errorValues"></param>
        /// <param name="objectiveFunction"></param>
        /// <returns></returns>
        private static double _tryToScaleSimplex(double scaleFactor, ref ErrorProfile errorProfile, Vector[] vertices,
                                                 double[] errorValues, ObjectiveFunctionDelegate objectiveFunction)
        {
            // find the centroid through which we will reflect
            var centroid = _computeCentroid(vertices, errorProfile);

            // define the vector from the centroid to the high point
            var centroidToHighPoint = vertices[errorProfile.HighestIndex].Subtract(centroid);

            // scale and position the vector to determine the new trial point
            var newPoint = centroidToHighPoint.Multiply(scaleFactor).Add(centroid);

            // evaluate the new point
            var newErrorValue = objectiveFunction(newPoint.Components);

            // if it's better, replace the old high point
            if (newErrorValue < errorValues[errorProfile.HighestIndex])
            {
                vertices[errorProfile.HighestIndex] = newPoint;
                errorValues[errorProfile.HighestIndex] = newErrorValue;
            }

            return newErrorValue;
        }

        /// <summary>
        /// Contract the simplex uniformly around the lowest point
        /// </summary>
        /// <param name="errorProfile"></param>
        /// <param name="vertices"></param>
        /// <param name="errorValues"></param>
        /// <param name="objectiveFunction"></param>
        private static void _shrinkSimplex(ErrorProfile errorProfile, Vector[] vertices, double[] errorValues,
                                           ObjectiveFunctionDelegate objectiveFunction)
        {
            var lowestVertex = vertices[errorProfile.LowestIndex];
            for (int i = 0; i < vertices.Length; i++)
            {
                if (i != errorProfile.LowestIndex)
                {
                    vertices[i] = (vertices[i].Add(lowestVertex)).Multiply(0.5);
                    errorValues[i] = objectiveFunction(vertices[i].Components);
                }
            }
        }

        /// <summary>
        /// Compute the centroid of all points except the worst
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="errorProfile"></param>
        /// <returns></returns>
        private static Vector _computeCentroid(Vector[] vertices, ErrorProfile errorProfile)
        {
            int numVertices = vertices.Length;
            // find the centroid of all points except the worst one
            var centroid = new Vector(numVertices - 1);
            for (int i = 0; i < numVertices; i++)
            {
                if (i != errorProfile.HighestIndex)
                {
                    centroid = centroid.Add(vertices[i]);
                }
            }
            return centroid.Multiply(1.0d/(numVertices - 1));
        }

        #region Nested type: ErrorProfile

        private sealed class ErrorProfile
        {
            public int HighestIndex { get; set; }
            public int NextHighestIndex { get; set; }
            public int LowestIndex { get; set; }
        }

        #endregion
    }
}