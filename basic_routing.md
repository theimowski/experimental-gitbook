# Basic routing

It's time to extend our WebPart to support multiple routes.
First, let's extract the WebPart and bind it to an identifier.
We can do that by typing:
`let webPart = OK "Hello World"`
and using the `webPart` identifier in our call to function `startWebServer`:
`startWebServer defaultConfig webPart`.
In C#, one would call it "assign webPart to a variable", but in functional world there's really no concept of a variable. Instead, we can "bind" a value to an identifier, which we can reuse later.
Value, once bound, can't be mutated during runtime.
Now, let's restrict our WebPart, so that the "Hello World" response is sent only at the root path of our application (`localhost:8083/` but not `localhost:8083/anything`):
`let webPart = path "/" >=> OK "Hello World"`
`path` function is defined in `Suave.Filters` module, thus we need to open it at the beggining of `App.fs`. `Suave.Operators` and `Suave.Successful` modules will also be crucial - let's open them as well.

<pre class="fssnip highlighted"><div lang="fsharp"><span class="k">open</span> <span onmouseout="hideTip(event, 'fs1', 1)" onmouseover="showTip(event, 'fs1', 1)" class="i">Suave</span>&#10;<span class="k">open</span> <span onmouseout="hideTip(event, 'fs1', 2)" onmouseover="showTip(event, 'fs1', 2)" class="i">Suave</span><span class="o">.</span><span onmouseout="hideTip(event, 'fs2', 3)" onmouseover="showTip(event, 'fs2', 3)" class="i">Filters</span>&#10;<span class="k">open</span> <span onmouseout="hideTip(event, 'fs1', 4)" onmouseover="showTip(event, 'fs1', 4)" class="i">Suave</span><span class="o">.</span><span onmouseout="hideTip(event, 'fs3', 5)" onmouseover="showTip(event, 'fs3', 5)" class="i">Operators</span>&#10;<span class="k">open</span> <span onmouseout="hideTip(event, 'fs1', 6)" onmouseover="showTip(event, 'fs1', 6)" class="i">Suave</span><span class="o">.</span><span onmouseout="hideTip(event, 'fs4', 7)" onmouseover="showTip(event, 'fs4', 7)" class="i">Successful</span>&#10;</div></pre>

`path` is a function of type:
`string -> WebPart`
It means that if we give it a string it will return WebPart.
Under the hood, the function looks at the incoming request and returns `Some` if the path matches, and `None` otherwise.
The `>=>` operator comes also from Suave library. It composes two WebParts into one by first evaluating the WebPart on the left, and applying the WebPart on the right only if the first one returned `Some`.

Let's move on to configuring a few routes in our application.
To achieve that, we can use the `choose` function, which takes a list of WebParts, and chooses the first one that applies (returns `Some`), or if none WebPart applies, then choose will also return `None`:

<pre class="fssnip highlighted"><div lang="fsharp"><span class="k">let</span> <span onmouseout="hideTip(event, 'fs5', 8)" onmouseover="showTip(event, 'fs5', 8)" class="f">webPart</span> <span class="o">=</span> &#10;    <span onmouseout="hideTip(event, 'fs6', 9)" onmouseover="showTip(event, 'fs6', 9)" class="f">choose</span> [&#10;        <span onmouseout="hideTip(event, 'fs7', 10)" onmouseover="showTip(event, 'fs7', 10)" class="f">path</span> <span class="s">"/"</span> <span class="o">&gt;</span><span class="o">=&gt;</span> (<span onmouseout="hideTip(event, 'fs8', 11)" onmouseover="showTip(event, 'fs8', 11)" class="f">OK</span> <span class="s">"Home"</span>)&#10;        <span onmouseout="hideTip(event, 'fs7', 12)" onmouseover="showTip(event, 'fs7', 12)" class="f">path</span> <span class="s">"/store"</span> <span class="o">&gt;</span><span class="o">=&gt;</span> (<span onmouseout="hideTip(event, 'fs8', 13)" onmouseover="showTip(event, 'fs8', 13)" class="f">OK</span> <span class="s">"Store"</span>)&#10;        <span onmouseout="hideTip(event, 'fs7', 14)" onmouseover="showTip(event, 'fs7', 14)" class="f">path</span> <span class="s">"/store/browse"</span> <span class="o">&gt;</span><span class="o">=&gt;</span> (<span onmouseout="hideTip(event, 'fs8', 15)" onmouseover="showTip(event, 'fs8', 15)" class="f">OK</span> <span class="s">"Store"</span>)&#10;        <span onmouseout="hideTip(event, 'fs7', 16)" onmouseover="showTip(event, 'fs7', 16)" class="f">path</span> <span class="s">"/store/details"</span> <span class="o">&gt;</span><span class="o">=&gt;</span> (<span onmouseout="hideTip(event, 'fs8', 17)" onmouseover="showTip(event, 'fs8', 17)" class="f">OK</span> <span class="s">"Details"</span>)&#10;    ]&#10;&#10;<span onmouseout="hideTip(event, 'fs9', 18)" onmouseover="showTip(event, 'fs9', 18)" class="f">startWebServer</span> <span onmouseout="hideTip(event, 'fs10', 19)" onmouseover="showTip(event, 'fs10', 19)" class="i">defaultConfig</span> <span onmouseout="hideTip(event, 'fs5', 20)" onmouseover="showTip(event, 'fs5', 20)" class="f">webPart</span>&#10;</div></pre>

<div class="tip" id="fs1">namespace Suave</div>
<div class="tip" id="fs2">module Filters<br /><br />from Suave</div>
<div class="tip" id="fs3">module Operators<br /><br />from Suave</div>
<div class="tip" id="fs4">module Successful<br /><br />from Suave</div>
<div class="tip" id="fs5">val webPart : WebPart&lt;HttpContext&gt;<br /><br />Full name: Basic-routing-gen.webPart</div>
<div class="tip" id="fs6">val choose : options:WebPart&lt;'a&gt; list -&gt; WebPart&lt;'a&gt;<br /><br />Full name: Suave.WebPart.choose</div>
<div class="tip" id="fs7">val path : pathAfterDomain:string -&gt; WebPart<br /><br />Full name: Suave.Filters.path</div>
<div class="tip" id="fs8">val OK : body:string -&gt; WebPart<br /><br />Full name: Suave.Successful.OK</div>
<div class="tip" id="fs9">val startWebServer : config:SuaveConfig -&gt; webpart:WebPart -&gt; unit<br /><br />Full name: Suave.Web.startWebServer</div>
<div class="tip" id="fs10">val defaultConfig : SuaveConfig<br /><br />Full name: Suave.Web.defaultConfig</div>
