import React from 'react';
import { storiesOf } from '@storybook/react';
import { action } from '@storybook/addon-actions';
import { withKnobs, boolean, text, select } from '@storybook/addon-knobs/react';
import withReadme from 'storybook-readme/with-readme';
import Readme from './README.md';
import { Button, Icons } from 'asc-web-components';
import Section from '../../../.storybook/decorators/section';
import { orderBy } from 'lodash';

storiesOf('Components|Buttons', module)
  .addDecorator(withKnobs)
  .addDecorator(withReadme(Readme))
  .add('base', () => {
    const sizeOptions = ['base', 'big'];
    const iconNames = orderBy(Object.keys(Icons), [name => name.toLowerCase()], ['asc']);

    const iconName = select("icon", ['', ...iconNames], '');
    const hintIcon = iconName ? React.createElement(Icons[iconName]) : undefined;

    return (
      <Section>
        <Button
          label={text('label', 'Base button')}
          primary={boolean('primary', true)}
          size={select('size', sizeOptions, 'big')}

          isLoading={boolean('isLoading', false)}
          isHovered={boolean('isHovered', false)}
          isClicked={boolean('isClicked', false)}
          isDisabled={boolean('isDisabled', false)}

          onClick={action('clicked')}

          icon={hintIcon}
        />
      </Section>
    )
  });
