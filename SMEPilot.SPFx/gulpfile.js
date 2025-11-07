'use strict';

const build = require('@microsoft/sp-build-web');

build.addSuppression(`Warning - [sass] The local CSS class 'ms-Grid' is not camelCase and will not be type-safe.`);

// Suppress known webpack warnings (SPFx 1.21+ should handle these, but keep as backup)
build.addSuppression(/Warning - \[webpack\].*toJson/);
build.addSuppression(/Error - \[webpack\].*toJson/);
build.addSuppression(/Error - \[webpack\].*asyncChunks/);

// Map 'serve' to 'serve-deprecated' for SPFx 1.21+
var getTasks = build.rig.getTasks;
build.rig.getTasks = function () {
  var result = getTasks.call(build.rig);
  var serveTask = result.get('serve-deprecated');
  if (serveTask) {
    result.set('serve', serveTask);
  }
  return result;
};

build.initialize(require('gulp'));

