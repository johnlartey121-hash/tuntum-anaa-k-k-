# Adds the LocalNet plugin and the custom view controller to the Xcode project (run from App/ios/App).
require 'xcodeproj'
project = Xcodeproj::Project.open('App.xcodeproj')
target = project.targets.find { |t| t.name == 'App' }
group = project.main_group.find_subpath('App', false)
%w[LocalNetPlugin.swift MainViewController.swift].each do |f|
  next if group.files.any? { |x| x.path == f }
  ref = group.new_reference(f)
  target.source_build_phase.add_file_reference(ref)
end
project.save
