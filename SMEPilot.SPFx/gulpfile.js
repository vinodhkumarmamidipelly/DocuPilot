'use strict';

const build = require('@microsoft/sp-build-web');

build.addSuppression(`Warning - [sass] The local CSS class 'ms-Grid' is not camelCase and will not be type-safe.`);

// SPFx 1.21+ should fix webpack issues, but keep suppression as backup
build.addSuppression(/Warning - \[webpack\].*toJson/);
build.addSuppression(/Error - \[webpack\].*toJson/);
build.addSuppression(/Error - \[webpack\].*asyncChunks/);

var getTasks = build.rig.getTasks;
build.rig.getTasks = function () {
  var result = getTasks.call(build.rig);

  result.set('serve', result.get('serve-deprecated'));

  return result;
};

build.initialize(require('gulp'));


