require 'yaml'
require 'pathname'
require 'uri'

root = Pathname.new(__dir__).parent.expand_path
errors = []
Dir.chdir(root) do
  docs = Dir['knowledge/**/*.md'] + Dir['.agents/skills/*/SKILL.md'] + %w[README.md AGENTS.md design/decision.md]
  docs.each do |path|
    text = File.read(path)
    keys = if path.start_with?('.agents/')
             %w[name description]
           elsif path.start_with?('knowledge/') && !%w[index.md log.md].include?(File.basename(path))
             %w[type]
           else
             []
           end
    unless keys.empty?
      metadata = YAML.safe_load(text[/\A---\r?\n(.*?)\r?\n---(?:\r?\n|\z)/m, 1].to_s)
      keys.each do |key|
        errors << "#{path}: missing #{key}" unless metadata.is_a?(Hash) && metadata[key].is_a?(String) && !metadata[key].strip.empty?
      end
    end
    text.scan(/\]\(([^)\s]+)\)/).flatten.each do |target|
      next if target.start_with?('#') || target.match?(/\A[a-z][a-z0-9+.-]*:/i)
      dest = URI::DEFAULT_PARSER.unescape(target.split('#', 2).first)
      errors << "#{path}: missing #{dest}" unless File.exist?(File.join(File.dirname(path), dest))
    end
  end
  puts "Checked #{docs.size} documents."
end
abort errors.join("\n") unless errors.empty?
