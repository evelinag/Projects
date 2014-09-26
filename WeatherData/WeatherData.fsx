(*** hide ***)
#I ".."
#load "packages/FsLab.0.0.19/FsLab.fsx"
#I "packages/Ariadne.0.1.1/lib/net40/"

#r "Ariadne.dll"
open Ariadne.GaussianProcess

// Data downloaded on 15 September 2014.
// Hard-coded here just for consistency
let data = 
    [|{Locations = [|3.0; 4.0; 5.0; 6.0; 7.0; 8.0; 9.0; 10.0; 11.0; 12.0; 13.0; 14.0; 15.0;
        16.0; 17.0; 18.0; 19.0; 20.0; 21.0; 22.0; 23.0; 24.0; 25.0; 26.0; 27.0;
        28.0; 29.0|];
     Observations =
      [|13.23; 14.98; 16.23; 14.9; 15.14; 14.42; 16.23; 16.58; 19.94; 18.09;
        18.08; 18.7; 16.12; 19.49; 19.18; 18.53; 18.22; 18.53; 18.1; 17.86;
        19.09; 17.13; 14.09; 18.59; 16.87; 17.37; 12.74|];};
    {Locations =
      [|0.0; 1.0; 2.0; 3.0; 4.0; 5.0; 6.0; 7.0; 8.0; 9.0; 10.0; 11.0; 12.0;
        13.0; 14.0; 15.0; 16.0; 17.0; 18.0; 19.0; 20.0; 21.0; 22.0; 23.0; 24.0;
        25.0; 26.0; 27.0; 28.0|];
     Observations =
      [|16.74; 15.4; 14.63; 13.58; 14.18; 15.05; 14.62; 15.19; 14.62; 14.8;
        16.5; 17.61; 17.77; 17.49; 18.68; 16.17; 16.88; 17.07; 18.07; 16.51;
        17.61; 16.57; 15.41; 15.97; 14.98; 14.05; 16.0; 15.93; 16.56|];};
    {Locations =
      [|3.0; 4.0; 5.0; 6.0; 7.0; 8.0; 9.0; 10.0; 11.0; 12.0; 13.0; 14.0; 15.0;
        16.0; 17.0; 18.0; 19.0; 20.0; 21.0; 22.0; 23.0; 24.0; 25.0; 26.0; 27.0;
        28.0; 29.0|];
     Observations =
      [|15.32; 13.23; 14.57; 13.1; 13.16; 13.81; 15.24; 15.03; 18.2; 16.76;
        16.58; 16.6; 15.28; 16.75; 17.08; 16.88; 16.08; 16.64; 16.18; 14.99;
        15.17; 14.95; 12.53; 16.56; 14.96; 15.38; 12.07|];};
    {Locations =
      [|1.0; 2.0; 3.0; 4.0; 5.0; 9.0; 10.0; 12.0; 17.0; 18.0; 19.0; 20.0; 24.0;
        25.0; 26.0; 27.0; 28.0|];
     Observations =
      [|16.14; 13.71; 13.59; 11.76; 13.73; 15.57; 15.7; 15.87; 18.83; 17.16;
        18.56; 17.9; 18.03; 16.18; 14.0; 17.15; 17.43|];};
    {Locations =
      [|0.0; 1.0; 2.0; 3.0; 4.0; 5.0; 9.0; 10.0; 11.0; 12.0; 13.0; 14.0; 15.0;
        16.0; 17.0; 18.0; 19.0; 20.0; 21.0; 22.0; 23.0; 24.0; 25.0; 26.0; 27.0|];
     Observations =
      [|14.0; 17.53; 15.45; 16.09; 15.97; 15.73; 16.54; 18.41; 20.03; 18.51;
        18.16; 20.58; 17.46; 19.0; 19.57; 19.84; 17.94; 17.53; 18.31; 19.0;
        18.39; 18.81; 16.0; 18.14; 18.24|];}  |]

open FSharp.Charting
data
|> Array.map (fun ds -> 
    let ts = Array.zip (ds.Locations) (ds.Observations)
    Chart.Point(ts, MarkerSize=10))
|> Chart.Combine
|> Chart.WithYAxis(Min=10.0, Max=25.0, Title="Temperature", TitleFontSize=14.0)
|> Chart.WithXAxis(Title="Days", TitleFontSize=14.0)

(**

Analyzing temperature data with Ariadne
========================

by [Evelina Gabasova](http://evelinag.com)

This [FsLab journal](http://visualstudiogallery.msdn.microsoft.com/45373b36-2a4c-4b6a-b427-93c7a8effddb)
shows an example usage of [Ariadne](http://evelinag.com/Ariadne), 
F# library for Gaussian process regression.

Let's say I want to know how the weather was over the last few weeks in Cambridge, UK. 
Of course, I can download historical data from the [Open Weather Map](http://openweathermap.org/)
and analyse them. But what if I do not trust the summary data they provide
for cities? I want to go to the source and download actual data measured at individual weather
stations. If I download historical data from several nearby locations, I should be able to get
some idea about the weather in the area. 

Open Weather Map provides all their data in Json format, 
which means I can use [F# Data](http://fsharp.github.io/FSharp.Data/) 
to access all the information.
First, we need to open all the necessary namespaces. I will also use
[F# Charting](http://fsharp.github.io/FSharp.Charting/) to generate some figures.
*)
#r "Ariadne.dll"
open Ariadne.GaussianProcess
open Ariadne.Kernels

open FSharp.Data
open FSharp.Charting

(**
Now I can download data directly from Open Weather Map. I create a query
which requests a specific number of weather stations which are closest to a specific 
location. Here I use Json type provider to load a list of 5 closest weather stations.
*)

// Json type provider 
type WeatherStations = JsonProvider<"http://api.openweathermap.org/data/2.5/station/find?lat=52.2&lon=0.12&cnt=5">

// Cambridge location
let latitude = 52.2
let longitude = 0.12
let count = 5

// load data from Open Weather Map
let query = @"http://api.openweathermap.org/data/2.5/station/find?lat=" 
            + string latitude + @"&lon=" + string longitude + "&cnt=" + string count
let stations = WeatherStations.Load(query)

(**
We can also check how far are the weather stations from Cambridge. It seems that
they all lie approximately within 15 miles from Cambridge.
*)

(*** define-output:distances ***)
stations 
|> Array.iter (fun station -> printfn "%.2f" (float station.Distance))
(*** include-output:distances ***)

(**
Now we send a query to Open Weather Map asking for historical data for each of 
the five weather stations. The constructed query should return summarized daily weather
data approximately for the past month.
Again, I am using Json type provider to access the data.
*)
type StationData = JsonProvider<"http://api.openweathermap.org/data/2.5/history/station?id=51473">

let stationIds = stations |> Array.map (fun station -> station.Station.Id)

let stationQuery (station:int) = 
    @"http://api.openweathermap.org/data/2.5/history/station?id=" + string station

let stationData = 
    stationIds
    |> Array.map (fun id -> StationData.Load(stationQuery id))

(**
After this step, we should have all the historical data for each station in ``stationData``.
I am interested in temperatures over the past month, so I extract only the average 
temperatures for each day. In general, station data contain also other information 
like atmospheric pressure, precipitation or wind speed.
In the following code, I extract the temperature in Celsius, and return
it together with its corresponding time of measurement.
*)
(*** define-output:temperatures ***)
// Extract temperatures
let temperatures = 
    stationData
    |> Array.map (fun s ->
        s.List |> Array.map (fun dt -> 
           // raw temperatures are in Kelvin, transform to Celsius
           dt.Dt , (float dt.Temp.V) - 273.15))

// Plot temperature data
temperatures
|> Array.map (fun ts -> Chart.Point(ts, MarkerSize=10))
|> Chart.Combine
|> Chart.WithYAxis(Min=10.0, Max=25.0)
(*** include-it:temperatures***)

(**
Notice that the data are quite noisy. Also, we have different number of observations 
from different weather stations. 
Each day, sometimes there are 5 observations, sometimes only 2, sometimes there are
outliers. Gaussian processes are a great tool to model this form of data because they take
all the noise and uncertainty into account. 

Another thing to notice is that Open Weather Map provides all date and time 
information in unix time.
To bring all the time into a more interpretable range, I change the scale 
into days. I also create an F# record containing ``Locations`` and ``Observations``
which is used by [Ariadne](http://evelinag.com/Ariadne)
to pass data into Gaussian process. 
*)
(*** do-not-eval ***)
let startTime = 
    temperatures |> Array.map (fun ts -> Array.minBy fst ts |> fst)
    |> Array.min

let data = 
    temperatures
    |> Array.map (fun temps ->
        {Locations = 
            Array.map (fun (t,_) -> 
                float (t - startTime) / (24.0 * 3600.0)) temps
         Observations = Array.map snd temps})

(**
Now we can construct a Gaussian process with squared exponential covariance function.
Squared exponential kernel generally characterizes very smooth functions. 
*)

let lengthscale = 1.0
let signalVariance = 20.0
let noiseVariance = 1.0

let sqExp = SquaredExp.SquaredExp(lengthscale, signalVariance, noiseVariance)

(**
I guessed some values for hyperparameters of the squared exponential covariance function. 
The first hyperparameter is the lengthscale. Its value regulates how quickly the function
values change, how wide squiggles we expect to see in the function. Lengthscale of 1
assumes that the temperature changes quite quickly from day to day.
 
The second parameter is the signal variance which regulates how far does the function
go from its prior mean. Since the prior mean function of our Gaussian process is zero
and I am being lazy by leaving the data on their original scale, I have to set quite a 
large variance. The last parameter is the noise variance which models the amount of 
noise present in the observed data. You can find more details on interpretation
of each hyperparameter in the 
[documentation](http://evelinag.com/Ariadne/covarianceFunctions.html).

With hyperparameters prepared, we can finally fit the Gaussian process regression model.
*)

(*** define-output:gp1 ***)
let gp = sqExp.GaussianProcess()
gp |> plot data
(*** include-it:gp1 ***)

(**
The graph shows the mean estimated temperature as a blue line. The grey region
corresponds to a 95% confidence interval. The fit is quite good, the confidence
interval captures most of the observed data and the mean captures the general trend.
You can see that the function goes to zero rapidly outside of the observed region.
This is because the Gaussian process prediction reverts to the prior mean value 
when there are no observations. Note that the confidence interval is also getting much
wider. Generally we can reliably extrapolate only about
one lengthscale outside of the data regions.

Optimizing hyperparameters
-----------------------------

Guessing values hyperparameters is not a very reliable method of fitting a model. Ariadne
provides two basic methods for [optimizing hyperparameters](http://evelinag.com/Ariadne/optimization.html):

 * Metropolis-Hastings posterior sampling
 * simple gradient descent

Both methods are optimizing the log likelihood of the hyperparameters given
observed data.
The first method, Metropolis-Hastings, is a probabilistic method which gives us
the mean estimate of the posterior distribution given observations and our
prior beliefs about hyperparameter values. This is the proper Bayesian method 
for optimizing hyperparameters. 

First we need to set prior distributions for each hyperparameter. Here I am using
a Log-normal prior because all hyperparamters have to be larger than zero. The prior
distributions are centred around my initial guessed values.
*)
open MathNet.Numerics
open Ariadne.Optimization
open Ariadne.Optimization.MetropolisHastings

let rnd = System.Random(0)
let lengthscalePrior = Distributions.LogNormal.WithMeanVariance(1.0, 1.0, rnd)
let variancePrior = Distributions.LogNormal.WithMeanVariance(20.0, 5.0, rnd)
let noisePrior = Distributions.LogNormal.WithMeanVariance(0.5, 1.0, rnd)
// construct the prior distribution
let prior = SquaredExp.Prior(lengthscalePrior, variancePrior, noisePrior)

// set parameters for Metropolis-Hastings sampler
let settings = 
    { Burnin = 500       // Number of burn-in iterations
      Lag = 5            // Thinning of posterior samples
      SampleSize = 100 } // Number of thinned posterior samples

(**
Metropolis-Hastings sampler in [Ariadne](http://evelinag.com/Ariadne) samples from 
the posterior distribution and returns the mean estimate for each hyperparameter.
*)
(*** define-output:gpMH ***)
// Run the sampler
let kernelMH = SquaredExp.optimizeMetropolis data settings prior sqExp
// Construct updated Gaussian process
let gpMH = kernelMH.GaussianProcess()

gpMH |> plot data
|> Chart.WithXAxis(Min=0.0, Max = 30.0)

(*** define-output:logliksMH ***)
printfn "Original Gaussian process likelihood: %f" (gp.LogLikelihood data)
printfn "MH Gaussian process likelihood: %f" (gpMH.LogLikelihood data)
(*** include-output:logliksMH ***)

(*** include-it:gpMH ***)

(**
Log likelihood of the new Gaussian process increased quite significantly, which means
that the optimized Gaussian process provides a much better fit to the observed training
data than the original one.

The second option how to select values of hyperparameters is standard nonlinear optimization.
Ariadne currently implements only basic gradient descent algorithm. This method uses
derivatives of the log likelihood function to find a local optimum.
*)

(*** define-output:gpGD ***)
let gdSettings = 
    { GradientDescent.Iterations = 1000; 
      GradientDescent.StepSize = 0.1}
let gradientFunction parameters = SquaredExp.fullGradient data parameters

// Run gradient descent
let kernelGD = 
    gradientDescent gradientFunction gdSettings (sqExp.Parameters)
    |> SquaredExp.ofParameters

// Create optimized Gaussian process
let gpGD = kernelGD.GaussianProcess()

gpGD |> plotRange (0.0, 30.0) data

(*** define-output:logliksGD ***)
printfn "Original Gaussian process likelihood: %f" (gp.LogLikelihood data)
printfn "GD Gaussian process likelihood: %f" (gpGD.LogLikelihood data)
(*** include-output:logliksGD ***)

(*** include-it:gpGD ***)

(**
Gradient descent found different optimum than Metropolis-Hastings. The fit seems to capture
mainly the overall trend in the temperatures over the whole month.

The two methods generally yield different resulting models. 
Metropolis-Hastings takes into account our prior beliefs about hyperparameter values
and it also samples from the true posterior. Gradient descent finds only a single local
maximum of the nonlinear log likelihood function.

You can download the ``fsx`` source file for weather analysis from 
[GitHub](https://github.com/evelinag/Projects/tree/master/WeatherData).

*)