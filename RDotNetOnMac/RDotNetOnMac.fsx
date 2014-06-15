(*** hide ***)
#I ".."
#load "packages/FsLab.0.0.14-beta/FsLab.fsx"
(**

Setting up R.NET on Mac 
========================

This is an overview of the steps to set up [R.NET](https://rdotnet.codeplex.com/)
on a Mac running OS X Maverics that worked for me.

Step 1: Install dependencies
------------------------
Mono in 64-bit mode needs to be compiled from source. For this, 
follow instructions from the [Mono website](http://www.mono-project.com/Compiling_Mono_on_OSX).

Before the actuall build, it is necessary to install `autoconf`, `automake` and 
`libtool`.

    [lang=bash]
    PREFIX=/usr/local
 
    # Ensure you have write permissions to /usr/local
    mkdir $PREFIX
    sudo chown -R `whoami` $PREFIX
 
    # Download and build dependencies
    mkdir ~/Build
    cd ~/Build
    curl -O ftp://ftp.gnu.org/gnu/autoconf/autoconf-2.69.tar.gz
    curl -O ftp://ftp.gnu.org/gnu/automake/automake-1.14.tar.gz
    curl -O ftp://ftp.gnu.org/gnu/libtool/libtool-2.4.2.tar.gz
 
    for i in *.tar.gz; do tar xzvf $i; done
    for i in */configure; do (cd `dirname $i`; ./configure --prefix=$PREFIX && make && make install); done


Add their locations to `PATH` so that the Mono installer finds them.

    [lang=bash]
    export PATH=$PREFIX/bin:$PATH

Step 2: Install 64-bit Mono
--------------------------------

Now we can install 64-bit Mono into a folder `MONO_PREFIX`:

    [lang=bash]
    MONO_PREFIX=$PREFIX/mono64
    git clone https://github.com/mono/mono.git
    cd mono
    ./autogen.sh --prefix=$MONO_PREFIX --disable-nls 
    make
    make install

Now we can run Mono in 64-bit using `/usr/local/mono64/bin/mono`.

**Optional** Create a symbolic link to the new 64-bit Mono installation.

    [lang=bash]
    ln -s /usr/local/mono64 /Library/Frameworks/Mono.framework/Versions/3.6.1

Step 3: Install R and set up R.NET
------------------------------------

Download and install [R](http://www.r-project.org/index.html). R for Mac is 
64-bit by default. 

We also need to set `LD_LIBRARY_PATH` and `PATH` to let R.NET know the location
of `libR.dylib`.

    [lang=bash]
    export LD_LIBRARY_PATH=/Library/Frameworks/R.framework/Libraries/:$LD_LIBRARY_PATH
    export PATH=/Library/Frameworks/R.framework/Libraries/:$PATH

Step 4: Setting up Xamarin studio
---------------------------------

In this step we set up Xamarin studio to run F# interactive in 64-bit which will
allow us to use R.NET interactively. We can change how Xamarin studio runs F# interactive
under `Preferences > Other > F# Settings > F# interactive`.

By default, this points to a script file 
`/Library/Frameworks/Mono.framework/Versions/3.4.0/bin/fsharpi`
which uses 32-bit version of Mono to run F# interactive. Create a copy of the script file
and edit the last line to call 64-bit Mono and F# interactive for any CPU:

    [lang=bash]
    $EXEC /usr/local/mono64/bin/mono $DEBUG $MONO_OPTIONS /Library/Frameworks/Mono.framework/Versions/3.4.0/lib/mono/4.0/fsiAnyCpu.exe --exename:$(basename $0) "$@"

Save the new script file and enter its location into the Xamarin settings for
F# interactive.

Step 5: Run R.NET
------------------------------
Now it should be possible to run R.NET in Xamarin studio.
It seems that R.NET still requires user to specify location of R libraries.
This is a simple test code:
*)

// Location of R libraries
#I "/Library/Frameworks/R.framework/Libraries/"

#r "packages/R.NET.Community.1.5.15/lib/net40/RDotNet.dll"
#r "packages/R.NET.Community.1.5.15/lib/net40/RDotNet.NativeLibrary.dll"
open RDotNet
open System

// Pass location of libR.dylib to R engine
let dllStr = "/Library/Frameworks/R.framework/Libraries/libR.dylib"
let engine = REngine.GetInstance(dll=dllStr)
engine.Initialize()

// Run a simple t-test

let group1 = engine.CreateNumericVector([| 30.02; 29.99; 30.11; 29.97; 30.01; 29.99 |])
engine.SetSymbol("group1", group1)

// Direct parsing from R script.
let group2 = engine.Evaluate("group2 <- c(29.89, 29.93, 29.72, 29.98, 30.02, 29.98)").AsNumeric()

// Test difference of mean and get the P-value.
let testResult = engine.Evaluate("t.test(group1, group2)").AsList()
let p = testResult.["p.value"].AsNumeric().[0]

Console.WriteLine("P-value = {0:0.000}", p)
